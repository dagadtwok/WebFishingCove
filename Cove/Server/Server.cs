using Steamworks;
using Cove.Server.Plugins;
using Cove.GodotFormat;
using Cove.Server.Actor;
using Cove.Server.Utils;
using Microsoft.Extensions.Hosting;
using Cove.Server.HostedServices;
using Microsoft.Extensions.Logging;
using System.Reflection;

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
        
        public bool hideJoinMessage = false;

        public float rainMultiplyer = 1f;
        public bool shouldSpawnMeteor = true;
        public bool shouldSpawnMetal = true;
        public bool shouldSpawnPortal = true;

        List<string> Admins = new();
        public CSteamID SteamLobby;

        public List<WFPlayer> AllPlayers = new();
        public List<WFActor> serverOwnedInstances = new();

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

                    case "hideJoinMessage":
                        hideJoinMessage = getBoolFromString(config[key]);
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
            Logger<HLSServerListService> HLSServerListLogger = new Logger<HLSServerListService>(loggerFactory);

            // Create the services that we need to run.
            IHostedService actorUpdateService = new ActorUpdateService(actorServiceLogger, this);
            IHostedService hostSpawnService = new HostSpawnService(hostSpawnServiceLogger, this);
            IHostedService hostSpawnMetalService = new HostSpawnMetalService(hostSpawnMetalServiceLogger, this);
            IHostedService hlsServerList = new HLSServerListService(HLSServerListLogger, this);

            // Start the services.
            actorUpdateService.StartAsync(CancellationToken.None);
            hostSpawnService.StartAsync(CancellationToken.None);
            hostSpawnMetalService.StartAsync(CancellationToken.None);
            hlsServerList.StartAsync(CancellationToken.None);

            // add them to the services dictionary so we can access them later if needed
            services["actor_update"] = actorUpdateService;
            services["host_spawn"] = hostSpawnService;
            services["host_spawn_metal"] = hostSpawnMetalService;
            services["hls_server_list"] = hlsServerList;

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
                SteamNetworking.AllowP2PPacketRelay(false);
                SteamMatchmaking.SetLobbyData(SteamLobby, "server_browser_value", "0");
                Console.WriteLine("Lobby Created!");
                Console.Write("Lobby Code: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(LobbyCode);
                Console.ResetColor();
                // set the player count in the title
                updatePlayercount();
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

                    WFPlayer newPlayer = new WFPlayer(userChanged, Username);
                    AllPlayers.Add(newPlayer);

                    //Console.WriteLine($"{Username} has been assigned the fisherID: {newPlayer.FisherID}");

                    foreach (PluginInstance plugin in loadedPlugins)
                    {
                        plugin.plugin.onPlayerJoin(newPlayer);
                    }

                    // check if the player is banned
                    if (isPlayerBanned(userChanged))
                        sendBlacklistPacketToAll(userChanged.m_SteamID.ToString()); // tell all players to blacklist the banned player

                }

                if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft) || stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected))
                {

                    string Username = SteamFriends.GetFriendPersonaName(userChanged);

                    Console.WriteLine($"{Username} [{userChanged.m_SteamID}] has left the game!");
                    updatePlayercount();

                    foreach (var player in AllPlayers)
                    {
                        if (player.SteamId.m_SteamID == userChanged.m_SteamID)
                        {

                            foreach (PluginInstance plugin in loadedPlugins)
                            {
                                plugin.plugin.onPlayerLeave(player);
                            }

                            AllPlayers.Remove(player);
                            Console.WriteLine($"{Username} has been removed!");
                        }
                    }
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

            // create the server
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MaxPlayers);

        }
        private bool getBoolFromString(string str)
        {
            if (str.ToLower() == "true")
            {
                return true;
            }
            else if (str.ToLower() == "false")
            {
                return false;
            }
            else
            {
                return false;
            }
        }

        void runSteamworksUpdate()
        {
            while (true)
            {
                SteamAPI.RunCallbacks();
            }
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
                    Console.WriteLine("-- Error responding to packet! --");
                    Console.WriteLine(e.ToString());
                }
            }
        }

        void OnPlayerChat(string message, CSteamID id)
        {

            WFPlayer sender = AllPlayers.Find(p => p.SteamId == id);
            Console.WriteLine($"{sender.Username}: {message}");

            foreach (PluginInstance plugin in loadedPlugins)
            {
                plugin.plugin.onChatMessage(sender, message);
            }
        }
    }
}
