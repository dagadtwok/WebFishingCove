using Cove.Server;
using Cove.Server.Actor;
using Cove.Server.Plugins;
using Steamworks;

public class ChatCommands : CovePlugin
{
    CoveServer Server { get; set; } // lol
    public ChatCommands(CoveServer server) : base(server)
    {
        Server = server;
    }

    public override void onInit()
    {
        base.onInit();
    }

    public override void onPlayerJoin(WFPlayer player)
    {
        base.onPlayerJoin(player);
    }

    public override void onChatMessage(WFPlayer sender, string message)
    {
        base.onChatMessage(sender, message);

        char[] msg = message.ToCharArray();
        if (msg[0] == "!".ToCharArray()[0]) // its a command!
        {
            string command = message.Split(" ")[0].ToLower();
            switch (command)
            {
                case "!help":
                    {
                        SendPlayerChatMessage(sender, "--- HELP ---");
                        SendPlayerChatMessage(sender, "!help - Shows this message");
                        SendPlayerChatMessage(sender, "!users - Shows all players in the server");
                        SendPlayerChatMessage(sender, "!spawn <actor> - Spawns an actor");
                        SendPlayerChatMessage(sender, "!kick <player> - Kicks a player");
                        SendPlayerChatMessage(sender, "!ban <player> - Bans a player");
                        SendPlayerChatMessage(sender, "!setjoinable <true/false> - Opens or closes the lobby");
                        SendPlayerChatMessage(sender, "!refreshadmins - Refreshes the admins list");
                    }
                    break;

                case "!users":
                    if (!IsPlayerAdmin(sender)) return;

                    // Get the command arguments
                    string[] commandParts = message.Split(' ');

                    int pageNumber = 1;
                    int pageSize = 10;

                    // Check if a page number was provided
                    if (commandParts.Length > 1)
                    {
                        if (!int.TryParse(commandParts[1], out pageNumber) || pageNumber < 1)
                        {
                            pageNumber = 1; // Default to page 1 if parsing fails or page number is less than 1
                        }
                    }

                    var allPlayers = GetAllPlayers();
                    int totalPlayers = allPlayers.Count();
                    int totalPages = (int)Math.Ceiling((double)totalPlayers / pageSize);

                    // Ensure the page number is within the valid range
                    if (pageNumber > totalPages) pageNumber = totalPages;

                    // Get the players for the current page
                    var playersOnPage = allPlayers.Skip((pageNumber - 1) * pageSize).Take(pageSize);

                    // Build the message to send
                    string messageBody = "";
                    foreach (var player in playersOnPage)
                    {
                        messageBody += $"\n{player.Username}: {player.FisherID}";
                    }

                    messageBody += $"\nPage {pageNumber} of {totalPages}";

                    SendPlayerChatMessage(sender, "Players in the server:" + messageBody + "\nAlways here - Cove");
                    break;

                case "!spawn":
                    {
                        if (!IsPlayerAdmin(sender)) return;

                        var actorType = message.Split(" ")[1].ToLower();
                        bool spawned = false;
                        switch (actorType)
                        {
                            case "rain":
                                Server.spawnRainCloud();
                                spawned = true;
                                break;

                            case "fish":
                                Server.spawnFish();
                                spawned = true;
                                break;

                            case "meteor":
                                spawned = true;
                                Server.spawnFish("fish_spawn_alien");
                                break;

                            case "portal":
                                Server.spawnVoidPortal();
                                spawned = true;
                                break;

                            case "metal":
                                Server.spawnMetal();
                                spawned = true;
                                break;
                        }
                        if (spawned)
                        {
                            SendPlayerChatMessage(sender, $"Spawned {actorType}");
                        }
                        else
                        {
                            SendPlayerChatMessage(sender, $"\"{actorType}\" is not a spawnable actor!");
                        }
                    }
                    break;

                case "!kick":
                    {
                        if (!IsPlayerAdmin(sender)) return;
                        string playerName = message.Substring(command.Length + 1);
                        WFPlayer kickedplayer = GetAllPlayers().ToList().Find(p => p.Username.Equals(playerName, StringComparison.OrdinalIgnoreCase));
                        if (kickedplayer == null)
                        {
                            SendPlayerChatMessage(sender, "That's not a player!");
                        }
                        else
                        {
                            Dictionary<string, object> packet = new Dictionary<string, object>();
                            packet["type"] = "kick";

                            SendPacketToPlayer(packet, kickedplayer);

                            SendPlayerChatMessage(sender, $"Kicked {kickedplayer.Username}");
                            SendGlobalChatMessage($"{kickedplayer.Username} was kicked from the lobby!");
                        }
                    }
                    break;
                    
                case "!ban":
                    {
                        if (!IsPlayerAdmin(sender)) return;
                        // hacky fix,
                        // Extract player name from the command message
                        string playerName = message.Substring(command.Length + 1);
                        WFPlayer playerToBan = GetAllPlayers().ToList().Find(p => p.Username.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                        if (playerToBan == null)
                        {
                            SendPlayerChatMessage(sender, "Player not found!");
                        }
                        else
                        {
                            BanPlayer(playerToBan);
                            SendPlayerChatMessage(sender, $"Banned {playerToBan.Username}");
                            SendGlobalChatMessage($"{playerToBan.Username} has been banned from the server.");
                        }
                    }
                    break;
                    
                case "!setjoinable":
                    {
                        if (!IsPlayerAdmin(sender)) return;
                        string arg = message.Split(" ")[1].ToLower();
                        if (arg == "true")
                        {
                            //Server.gameLobby.SetJoinable(true);
                            SteamMatchmaking.SetLobbyJoinable(Server.SteamLobby, true);
                            SendPlayerChatMessage(sender, $"Opened lobby!");
                            if (!Server.codeOnly)
                            {
                                //Server.gameLobby.SetData("type", "public");
                                SteamMatchmaking.SetLobbyData(Server.SteamLobby, "type", "public");
                                SendPlayerChatMessage(sender, $"Unhid server from server list");
                            }
                        }
                        else if (arg == "false")
                        {
                            //Server.gameLobby.SetJoinable(false);
                            SteamMatchmaking.SetLobbyJoinable(Server.SteamLobby, false);
                            SendPlayerChatMessage(sender, $"Closed lobby!");
                            if (!Server.codeOnly)
                            {
                                //Server.gameLobby.SetData("type", "code_only");
                                SteamMatchmaking.SetLobbyData(Server.SteamLobby, "type", "code_only");
                                SendPlayerChatMessage(sender, $"Hid server from server list");
                            }
                        }
                        else
                        {
                            SendPlayerChatMessage(sender, $"\"{arg}\" is not true or false!");
                        }
                    }
                    break;

                case "!refreshadmins":
                    {
                        if (!IsPlayerAdmin(sender)) return;
                        Server.readAdmins();
                    }
                    break;
            }
        }

    }

}
