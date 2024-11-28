﻿/*
   Copyright 2024 DrMeepso

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using Steamworks;
using Cove.Server.Plugins;
using Cove.Server.Actor;
using Cove.Server.Utils;
using Microsoft.Extensions.Hosting;
using Cove.Server.HostedServices;
using Microsoft.Extensions.Logging;
using Vector3 = Cove.GodotFormat.Vector3;

namespace Cove.Server
{
    public partial class CoveServer
    {
        public readonly string WebFishingGameVersion = "1.1"; // make sure to update this when the game updates!
        public int MaxPlayers = 20;
        public string ServerName = "A Cove Dedicated Server";
        public string LobbyCode = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
        public bool codeOnly = true;
        public bool ageRestricted = false;
        
        public string joinMessage = "This is a Cove dedicated server!\nPlease report any issues to the github (xr0.xyz/cove)";
        public bool displayJoinMessage = true;

        public float rainMultiplyer = 1f;
        public bool shouldSpawnMeteor = true;
        public bool shouldSpawnMetal = true;
        public bool shouldSpawnPortal = true;

        public bool showErrorMessages = true;
        public bool friendsOnly = false;
        public bool adminOnlyChalkPackets = false;

        List<string> Admins = new();
        public CSteamID SteamLobby;

        public List<WFPlayer> AllPlayers = new();
        public List<WFActor> serverOwnedInstances = new();
        public List<WFActor> allActors = new();

        Thread cbThread;
        Thread networkThread;

        List<Vector3> fish_points;
        List<Vector3> trash_points;
        List<Vector3> shoreline_points;
        List<Vector3> hidden_spot;

        Dictionary<string, IHostedService> services = new();

        public void Init()
        {
            cbThread = new(runSteamworksUpdate);
            networkThread = new(RunNetwork);

            Console.WriteLine("Loading world!");
            string worldFile = $"{AppDomain.CurrentDomain.BaseDirectory}worlds/main_zone.tscn";
            if (!File.Exists(worldFile))
            {
                Console.WriteLine("-- ERROR --");
                Console.WriteLine("main_zone.tscn is missing!");
                Console.WriteLine("please put a world file in the /worlds folder so the server may load it!");
                Console.WriteLine("-- ERROR --");
                Console.WriteLine("Press any key to exit");

                Console.ReadKey();

                return;
            }

            string banFile = $"{AppDomain.CurrentDomain.BaseDirectory}bans.txt";
            if (!File.Exists(banFile))
            {
                FileStream f = File.Create(banFile);
                f.Close(); // close the file
            }

            // get all the spawn points for fish!
            string mapFile = File.ReadAllText(worldFile);
            fish_points = WorldFile.readPoints("fish_spawn", mapFile);
            trash_points = WorldFile.readPoints("trash_point", mapFile);
            shoreline_points = WorldFile.readPoints("shoreline_point", mapFile);
            hidden_spot = WorldFile.readPoints("hidden_spot", mapFile);

            Console.WriteLine("World Loaded!");

            Console.WriteLine("Reading server.cfg");

            Dictionary<string, string> config = ConfigReader.ReadConfig("server.cfg");
            foreach (string key in config.Keys)
            {
                switch (key)
                {
                    case "serverName":
                        ServerName = config[key];
                        break;

                    case "maxPlayers":
                        MaxPlayers = int.Parse(config[key]);
                        break;

                    case "code":
                        LobbyCode = config[key].ToUpper();
                        break;

                    case "rainSpawnMultiplyer":
                        rainMultiplyer = float.Parse(config[key]);
                        break;

                    case "codeOnly":
                        codeOnly = getBoolFromString(config[key]);
                        break;

                    case "gameVersion":
                        //WebFishingGameVersion = config[key];
                        break;

                    case "ageRestricted":
                        ageRestricted = getBoolFromString(config[key]);
                        break;

                    case "pluginsEnabled":
                        arePluginsEnabled = getBoolFromString(config[key]);
                        break;

                    case "joinMessage":
                        joinMessage = config[key].Replace("\\n", "\n");
                        break;

                    case "spawnMeteor":
                        shouldSpawnMeteor = getBoolFromString(config[key]);
                        break;

                    case "spawnMetal":
                        shouldSpawnMetal = getBoolFromString(config[key]);
                        break;

                    case "spawnPortal":
                        shouldSpawnPortal = getBoolFromString(config[key]);
                        break;

                    case "showErrors":
                        showErrorMessages = getBoolFromString(config[key]);
                        break;

                    case "friendsOnly":
                        friendsOnly = getBoolFromString(config[key]);
                        break;

                    case "hideJoinMessage":
                        displayJoinMessage = !getBoolFromString(config[key]);
                        break;

                    case "adminOnlyChalkPackets":
                        adminOnlyChalkPackets = getBoolFromString(config[key]);
                        break;

                    default:
                        Console.WriteLine($"\"{key}\" is not a supported config option!");
                        continue;
                }

                Console.WriteLine($"Set \"{key}\" to \"{config[key]}\"");

            }

            Console.WriteLine("Server setup based on config!");

            Console.WriteLine("Reading admins.cfg");
            readAdmins();
            Console.WriteLine("Setup finished, starting server!");

            if (Directory.Exists($"{AppDomain.CurrentDomain.BaseDirectory}plugins"))
            {
                loadAllPlugins();
            }
            else
            {
                Directory.CreateDirectory($"{AppDomain.CurrentDomain.BaseDirectory}plugins");
                Console.WriteLine("Created plugins folder!");
            }

            if (!SteamAPI.Init())
            {
                Console.WriteLine("SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.");
                Console.WriteLine("Is Steam running?");
                return;
            }

            // thread for running steamworks callbacks
            cbThread.IsBackground = true;
            cbThread.Start();

            // thread for getting network packets from steam
            // i wish this could be a service, but when i tried it the packets got buffered and it was a mess
            // like 10 minutes of delay within 30 seconds
            networkThread.IsBackground = true;
            networkThread.Start();
            
            bool LogServices = false;
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                if (LogServices)
                    builder.AddConsole();
            });

            // Create a logger for each service that we need to run.
            Logger<ActorUpdateService> actorServiceLogger = new Logger<ActorUpdateService>(loggerFactory);
            Logger<HostSpawnService> hostSpawnServiceLogger = new Logger<HostSpawnService>(loggerFactory);
            Logger<HostSpawnMetalService> hostSpawnMetalServiceLogger = new Logger<HostSpawnMetalService>(loggerFactory);

            // Create the services that we need to run.
            IHostedService actorUpdateService = new ActorUpdateService(actorServiceLogger, this);
            IHostedService hostSpawnService = new HostSpawnService(hostSpawnServiceLogger, this);
            IHostedService hostSpawnMetalService = new HostSpawnMetalService(hostSpawnMetalServiceLogger, this);

            // Start the services.
            actorUpdateService.StartAsync(CancellationToken.None);
            hostSpawnService.StartAsync(CancellationToken.None);
            hostSpawnMetalService.StartAsync(CancellationToken.None);

            // add them to the services dictionary so we can access them later if needed
            services["actor_update"] = actorUpdateService;
            services["host_spawn"] = hostSpawnService;
            services["host_spawn_metal"] = hostSpawnMetalService;

            Callback<LobbyCreated_t>.Create((LobbyCreated_t param) =>
            {
                SteamLobby = new CSteamID(param.m_ulSteamIDLobby);
                SteamMatchmaking.SetLobbyData(SteamLobby, "ref", "webfishing_gamelobby");
                SteamMatchmaking.SetLobbyData(SteamLobby, "version", WebFishingGameVersion);
                SteamMatchmaking.SetLobbyData(SteamLobby, "code", LobbyCode);
                SteamMatchmaking.SetLobbyData(SteamLobby, "type", codeOnly ? "code_only" : "public");
                SteamMatchmaking.SetLobbyData(SteamLobby, "public", "true");
                SteamMatchmaking.SetLobbyData(SteamLobby, "banned_players", "");
                SteamMatchmaking.SetLobbyData(SteamLobby, "age_limit", ageRestricted ? "true" : "false");
                SteamMatchmaking.SetLobbyData(SteamLobby, "cap", MaxPlayers.ToString());
                SteamNetworking.AllowP2PPacketRelay(true);
                SteamMatchmaking.SetLobbyData(SteamLobby, "server_browser_value", "0");
                Console.WriteLine("Lobby Created!");
                Console.Write("Lobby Code: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(LobbyCode);
                Console.ResetColor();
                // set the player count in the title
                updatePlayercount();

                SteamFriends.SetRichPresence("steam_display", $"hosting a server");
            });

            Callback<LobbyChatUpdate_t>.Create((LobbyChatUpdate_t param) =>
            {

                CSteamID lobbyID = new CSteamID(param.m_ulSteamIDLobby);

                CSteamID userChanged = new CSteamID(param.m_ulSteamIDUserChanged);
                CSteamID userMakingChange = new CSteamID(param.m_ulSteamIDMakingChange);

                EChatMemberStateChange stateChange = (EChatMemberStateChange)param.m_rgfChatMemberStateChange;
                if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeEntered))
                {
                    string Username = SteamFriends.GetFriendPersonaName(userChanged);

                    Console.WriteLine($"{Username} [{userChanged.m_SteamID}] has joined the game!");
                    updatePlayercount();

                    if (AllPlayers.Find(p => p.SteamId.m_SteamID == userChanged.m_SteamID) != null)
                    {
                        Console.WriteLine($"{Username} is already in the server, rejecting");
                        sendBlacklistPacketToAll(userChanged.m_SteamID.ToString()); // tell players to blacklist the player
                        return; // player is already in the server, dont add them again
                    }

                    WFPlayer newPlayer = new WFPlayer(userChanged, Username);
                    AllPlayers.Add(newPlayer);

                    foreach (PluginInstance p in loadedPlugins)
                    {
                        p.plugin.onPlayerJoin(newPlayer);
                    }

                    // check if the player is banned
                    if (isPlayerBanned(userChanged))
                        sendBlacklistPacketToAll(userChanged.m_SteamID.ToString()); // tell all players to blacklist the banned player

                    if (userChanged.m_SteamID == SteamUser.GetSteamID().m_SteamID)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The account running the server has joined the game!");
                        Console.WriteLine("This will cause issues, please run the server on a different account!");
                        Console.ResetColor();
                    }

                }

                if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft) || stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected))
                {
                    string Username = SteamFriends.GetFriendPersonaName(userChanged);

                    Console.WriteLine($"{Username} [{userChanged.m_SteamID}] has left the game!");
                    updatePlayercount();

                    WFPlayer leavingPlayer = AllPlayers.Find(p => p.SteamId.m_SteamID == userChanged.m_SteamID);
                    foreach (PluginInstance plugin in loadedPlugins)
                    {
                        plugin.plugin.onPlayerLeave(leavingPlayer);
                    }
                    AllPlayers.Remove(leavingPlayer);
                    allActors.RemoveAll(a => a.owner.m_SteamID == userChanged.m_SteamID);
                }
            });

            Callback<P2PSessionRequest_t> callback = Callback<P2PSessionRequest_t>.Create((P2PSessionRequest_t param) =>
            {

                // get all members in the lobby
                CSteamID[] members = getAllPlayers();
                if (!members.Contains(param.m_steamIDRemote) && AllPlayers.Find(p => p.SteamId.m_SteamID == param.m_steamIDRemote.m_SteamID) == null)
                {
                    Console.WriteLine($"Got P2P request from {param.m_steamIDRemote}, but they are not in the lobby!");
                    SteamNetworking.CloseP2PSessionWithUser(param.m_steamIDRemote);
                    return;
                }

                if (isPlayerBanned(param.m_steamIDRemote))
                {
                    Console.WriteLine($"Got P2P request from {param.m_steamIDRemote}, but they are banned!");
                    SteamNetworking.CloseP2PSessionWithUser(param.m_steamIDRemote);
                    sendBlacklistPacketToAll(param.m_steamIDRemote.m_SteamID.ToString());
                    return;
                }

                SteamNetworking.AcceptP2PSessionWithUser(param.m_steamIDRemote);
            });

            if (friendsOnly)
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers);
            else
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MaxPlayers);
        }
        private bool getBoolFromString(string str)
        {
            if (str.ToLower() == "true")
                return true;
            else if (str.ToLower() == "false")
                return false;
            else
                return false;
        }

        void runSteamworksUpdate()
        {
            while (true)
                SteamAPI.RunCallbacks();
        }

        void RunNetwork()
        {
            while (true)
            {
                try
                {
                    // OnNetworkPacket(packet.Value);
                    for (int i = 0; i < 6; i++)
                    {
                        uint packetSize = 0;
                        // we are going to check if there are any incoming net packets!
                        if (SteamNetworking.IsP2PPacketAvailable(out packetSize, nChannel: i))
                        {
                            byte[] packet = new byte[packetSize];
                            uint bytesRead = 0;
                            CSteamID sender;

                            if (SteamNetworking.ReadP2PPacket(packet, packetSize, out bytesRead, out sender, nChannel: i))
                            {
                                OnNetworkPacket(packet, sender);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!showErrorMessages)
                        return;
                    
                    Console.WriteLine("-- Error responding to packet! --");
                    Console.WriteLine(e.ToString());
                }
            }
        }

        void OnPlayerChat(string message, CSteamID id)
        {

            WFPlayer sender = AllPlayers.Find(p => p.SteamId == id);
            if (sender == null)
            {
                Console.WriteLine($"[UNKNOWN] {id}: {message}");
                // should probbaly kick the player here
                return;
            }

            Console.WriteLine($"[{sender.FisherID}] {sender.Username}: {message}");

            foreach (PluginInstance plugin in loadedPlugins)
            {
                plugin.plugin.onChatMessage(sender, message);
            }
        }
    }
}
