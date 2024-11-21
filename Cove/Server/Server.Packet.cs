using Steamworks;
using Cove.GodotFormat;
using Cove.Server.Actor;
using Cove.Server.Utils;

namespace Cove.Server
{
    partial class CoveServer
    {

        // TODO: Make this a switch statement
        void OnNetworkPacket(byte[] packet, CSteamID sender)
        {
            Dictionary<string, object> packetInfo = readPacket(GzipHelper.DecompressGzip(packet));

            // just in case!
            if (isPlayerBanned(sender))
                banPlayer(sender);

            switch ((string)packetInfo["type"])
            {
                case "handshake_request":
                    {
                        Dictionary<string, object> handshakePacket = new();
                        handshakePacket["type"] = "handshake";
                        handshakePacket["user_id"] = SteamUser.GetSteamID().m_SteamID.ToString();
                        sendPacketToPlayer(handshakePacket, sender);
                    }
                    break;

                case "new_player_join":
                    {
                        if (!string.IsNullOrEmpty(joinMessage))
                        {
                            messagePlayer(joinMessage, sender);
                        }
                        Dictionary<string, object> hostPacket = new();
                        hostPacket["type"] = "recieve_host";
                        hostPacket["host_id"] = SteamUser.GetSteamID().m_SteamID.ToString();
                        sendPacketToPlayers(hostPacket);
                        if (isPlayerAdmin(sender))
                        {
                            messagePlayer("You're an admin on this server!", sender);
                        }

                        // send the player all the canvas data
                        foreach (Chalk.ChalkCanvas canvas in chalkCanvas)
                        {
                            var chalkPacket = new Dictionary<string, object> { { "type", "chalk_packet" }, { "canvas_id", canvas.canvasID }, { "data", canvas.getChalkPacket() } };
                            printStringDict(chalkPacket);

                            sendPacketToPlayer(chalkPacket, sender);
                        }

                    }
                    break;

                case "instance_actor":
                    {
                        string type = (string)((Dictionary<string, object>)packetInfo["params"])["actor_type"];
                        long actorID = (long)((Dictionary<string, object>)packetInfo["params"])["actor_id"];

                        // all actor types that should not be spawned by anyone but the server!
                        if (type == "fish_spawn_alien" || type == "fish_spawn" || type == "raincloud")
                        {
                            WFPlayer offendingPlayer = AllPlayers.Find(p => p.SteamId.m_SteamID == sender.m_SteamID);

                            // kick the player because the spawned in a actor that only the server should be able to spawn!
                            Dictionary<string, object> kickPacket = new Dictionary<string, object>();
                            kickPacket["type"] = "kick";

                            sendPacketToPlayer(kickPacket, sender);

                            messageGlobal($"{offendingPlayer.Username} was kicked for spawning illegal actors");
                        }

                        if (type == "player")
                        {
                            WFPlayer thisPlayer = AllPlayers.Find(p => p.SteamId.m_SteamID == sender.m_SteamID);
                            if (thisPlayer == null)
                            {
                                Console.WriteLine("No fisher found for player instance!");
                            }
                            else
                            {
                                thisPlayer.InstanceID = actorID;
                            }
                        }

                    }
                    break;

                case "actor_update":
                    {
                        WFPlayer thisPlayer = AllPlayers.Find(p => p.InstanceID == (long)packetInfo["actor_id"]);
                        if (thisPlayer != null)
                        {
                            Vector3 position = (Vector3)packetInfo["pos"];
                            thisPlayer.pos = position;
                        }
                    }
                    break;

                case "request_ping":
                    {
                        Dictionary<string, object> pongPacket = new();
                        pongPacket["type"] = "send_ping";
                        pongPacket["time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                        pongPacket["from"] = SteamUser.GetSteamID().m_SteamID.ToString();
                        // send the ping packet!
                        //SteamNetworking.SendP2PPacket(packet.SteamId, writePacket(pongPacket), nChannel: 1);
                        sendPacketToPlayer(pongPacket, sender);
                    }
                    break;

                case "actor_action":
                    {
                        if ((string)packetInfo["action"] == "_sync_create_bubble")
                        {
                            string Message = (string)((Dictionary<int, object>)packetInfo["params"])[0];
                            OnPlayerChat(Message, sender);
                        }
                        if ((string)packetInfo["action"] == "_wipe_actor")
                        {
                            long actorToWipe = (long)((Dictionary<int, object>)packetInfo["params"])[0];
                            WFActor serverInst = serverOwnedInstances.Find(i => i.InstanceID == actorToWipe);
                            if (serverInst != null)
                            {
                                // stop players from removing rain clouds
                                if (serverInst.Type == "raincloud")
                                    return;

                                Console.WriteLine($"Player asked to remove {serverInst.Type} actor");

                                // the sever owns the instance
                                removeServerActor(serverInst);
                            }
                        }
                    }
                    break;

                case "request_actors":
                    {
                        sendPlayerAllServerActors(sender);
                        sendPacketToPlayer(createRequestActorResponce(), sender); // this is empty because otherwise all the server actors are invisible!
                    }
                    break;

                case "chalk_packet":
                    {
                        long canvasID = (long)packetInfo["canvas_id"];
                        Chalk.ChalkCanvas canvas = chalkCanvas.Find(c => c.canvasID == canvasID);
                        
                        if (canvas == null)
                        {
                            Console.WriteLine("Creating new canvas");
                            canvas = new Chalk.ChalkCanvas(canvasID);
                            chalkCanvas.Add(canvas);
                        }

                        canvas.chalkUpdate((Dictionary<int, object>)packetInfo["data"]);

                    }
                    break;
            }
        }
    }
}
