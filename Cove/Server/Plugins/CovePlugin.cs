using Cove.Server.Actor;

namespace Cove.Server.Plugins
{
    public class CovePlugin
    {

        private CoveServer parentServer;

        public CovePlugin(CoveServer parent)
        {
            parentServer = parent;
        }

        // triggered when the plugin is started
        public virtual void onInit() { }
        // triggerd 12/s
        public virtual void onUpdate() { }
        // triggered when a player speaks in anyway (exluding / commands)
        public virtual void onChatMessage(WFPlayer sender, string message) { }
        // triggerd when a player enters the server
        public virtual void onPlayerJoin(WFPlayer player) { }
        // triggered when a player leaves the server
        public virtual void onPlayerLeave(WFPlayer player) { }

        public WFPlayer[] GetAllPlayers()
        {
            return parentServer.AllPlayers.ToArray();
        }

        public void SendPlayerChatMessage(WFPlayer receiver, string message, string hexColor = "ffffff")
        {
            // remove a # incase its given
            parentServer.messagePlayer(message, receiver.SteamId, color: hexColor.Replace("#", ""));
        }

        public void SendGlobalChatMessage(string message, string hexColor = "ffffff")
        {
            parentServer.messageGlobal(message, color: hexColor.Replace("#", ""));
        }

        public WFActor[] GetAllServerActors()
        {
            return parentServer.serverOwnedInstances.ToArray();
        }

        public WFActor? GetActorFromID(int id)
        {
            return parentServer.serverOwnedInstances.Find(a => a.InstanceID == id);
        }

        // please make sure you use the correct actorname or the game freaks out!
        public WFActor SpawnServerActor(string actorType)
        {
            return parentServer.spawnGenericActor(actorType);
        }

        public void RemoveServerActor(WFActor actor)
        {
            parentServer.removeServerActor(actor);
        }

        // i on god dont know what this dose to the actual actor but it works in game, so if you nee this its here
        public void SetServerActorZone(WFActor actor, string zoneName, int zoneOwner)
        {
            parentServer.setActorZone(actor, zoneName, zoneOwner);
        }

        public void KickPlayer(WFPlayer player)
        {
            parentServer.kickPlayer(player.SteamId);
        }

        public void BanPlayer(WFPlayer player)
        {
            if (parentServer.isPlayerBanned(player.SteamId))
            {
                parentServer.banPlayer(player.SteamId);
            } else
            {
                parentServer.banPlayer(player.SteamId, true); // save to file if they are not already in there!
            }
        }

        public void Log(string message)
        {
            parentServer.printPluginLog(message, this);
        }

        public bool IsPlayerAdmin(WFPlayer player)
        {
            return parentServer.isPlayerAdmin(player.SteamId);
        }

        public void SendPacketToPlayer(Dictionary<string, object> packet, WFPlayer player)
        {
            parentServer.sendPacketToPlayer(packet, player.SteamId);
        }

        public void SendPacketToAll(Dictionary<string, object> packet)
        {
            parentServer.sendPacketToPlayers(packet);
        }

    }
}
