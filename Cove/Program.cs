using Cove;
using Cove.Server;
using Cove.Server.Actor;
using Steamworks;

CoveServer webfishingServer = new CoveServer();

webfishingServer.Init(); // start the server

Console.CancelKeyPress += Console_CancelKeyPress;
void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    Console.WriteLine("Application is closing...");

    Dictionary<string, object> closePacket = new();
    closePacket["type"] = "server_close";

    webfishingServer.disconnectAllPlayers();
    SteamMatchmaking.LeaveLobby(webfishingServer.Lobby);
    SteamAPI.Shutdown();
}

while (true)
{
    string input = Console.ReadLine();
    string command = input.Split(' ')[0];

    switch(command)
    {
        case "exit":
            Console.WriteLine("Application is closing...");
            Dictionary<string, object> closePacket = new();
            closePacket["type"] = "server_close";

            webfishingServer.disconnectAllPlayers();
            SteamMatchmaking.LeaveLobby(webfishingServer.Lobby);
            SteamAPI.Shutdown();
            Environment.Exit(0);
            break;
        case "say":
            {
                string message = input.Substring(command.Length + 1);
                webfishingServer.messageGlobal($"Server: {message}");
                Console.WriteLine($"Server: {message}");
            }
            break;
        case "ban":
            {
                string id = input.Substring(command.Length + 1);
                WFPlayer player = webfishingServer.AllPlayers.Find(p => p.FisherName.ToLower() == id.ToLower());
                if (player != null)
                {
                    if (webfishingServer.isPlayerBanned(player.SteamId))
                    {
                        Console.WriteLine($"Player {player.FisherName} is already banned!");
                        break;
                    } else
                    {
                        webfishingServer.banPlayer(player.SteamId, true);
                    }
                    Console.WriteLine($"Banned player {player.FisherName}");
                }
                else
                {
                    Console.WriteLine("Player not found!");
                }
            }
            break;
        case "kick":
            {
                string id = input.Substring(command.Length + 1);
                WFPlayer player = webfishingServer.AllPlayers.Find(p => p.FisherName.ToLower() == id.ToLower());
                if (player != null)
                {
                    webfishingServer.kickPlayer(player.SteamId);
                    Console.WriteLine($"Kicked player {player.FisherName}");
                }
                else
                {
                    Console.WriteLine("Player not found!");
                }
            }
            break;
        case "players":
            Console.WriteLine("Players:");
            foreach (WFPlayer player in webfishingServer.AllPlayers)
            {
                Console.WriteLine(player.FisherName);
            }
            break;
        case "help":
            Console.WriteLine("Commands:");
            Console.WriteLine("exit - Closes the application");
            Console.WriteLine("say <message> - Sends a message to all players");
            Console.WriteLine("ban <player> - Bans a player");
            Console.WriteLine("kick <player> - Kicks a player");
            Console.WriteLine("help - Shows this message");
            Console.WriteLine("players - Lists all players");
            Console.WriteLine("");
            Console.WriteLine("players are the username of the player");
            break;
        default:
            Console.WriteLine("Unknown command! Type 'help' for a list of commands.");
            break;
    }

}