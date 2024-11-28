using Steamworks;
using Cove.GodotFormat;
using Cove.Server.Utils;
using Cove.Server.Actor;

namespace Cove.Server
{
    partial class CoveServer
    {
        Dictionary<string, object> readPacket(byte[] packetBytes)
        {
            return (new GodotReader(packetBytes)).readPacket();
        }

        byte[] writePacket(Dictionary<string, object> packet)
        {
            byte[] godotBytes = GodotWriter.WriteGodotPacket(packet);
            return GzipHelper.CompressGzip(godotBytes);
        }

        public void sendPacketToPlayers(Dictionary<string, object> packet)
        {
            byte[] packetBytes = writePacket(packet);
            
            foreach (CSteamID player in getAllPlayers().ToList())
            {
                if (player == SteamUser.GetSteamID())
                    continue;
                
                SteamNetworking.SendP2PPacket(player, packetBytes, (uint)packetBytes.Length, EP2PSend.k_EP2PSendReliable, nChannel: 2);
            }
        }

        public void sendPacketToPlayer(Dictionary<string, object> packet, CSteamID id)
        {
            byte[] packetBytes = writePacket(packet);
            SteamNetworking.SendP2PPacket(id, packetBytes, (uint)packetBytes.Length, EP2PSend.k_EP2PSendReliable, nChannel: 2);
        }

        public CSteamID[] getAllPlayers()
        {
            int playerCount = AllPlayers.Count;
            CSteamID[] players = new CSteamID[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                players[i] = AllPlayers[i].SteamId;
            }

            return players;
        }
    }
}
