using Ow.Game.Objects;
using Ow.Net.netty.commands;

namespace Ow.Net.netty
{
    static class PacketDebug
    {
        public static void NotifyIncoming(Player player, string packetName, short packetId)
        {
            Notify(player, "C2S", packetName, packetId);
        }

        public static void NotifyPortalCount(Player player, int mapId, int portalCount)
        {
            if (player == null)
                return;

            player.SendPacket($"0|A|STD|[PACKET_DEBUG] MAP_PORTALS MAP={mapId} COUNT={portalCount}");
        }

        public static void NotifyOutgoing(Player player, byte[] command)
        {
            if (command == null || command.Length < 4)
                return;

            var packetId = (short)((command[2] << 8) | (command[3] & 0xff));
            var packetName = GetOutgoingPacketName(packetId);
            if (packetName != null)
                Notify(player, "S2C", packetName, packetId);
        }

        private static string GetOutgoingPacketName(short packetId)
        {
            switch (packetId)
            {
                case CreateOreCommand.ID:
                    return "CreateOreCommand";
                case OreCountUpdateCommand.ID:
                    return "OreCountUpdateCommand";
                case OreCargoUpdateCommand.ID:
                    return "OreCargoUpdateCommand";
                case OreRefinementUpdateCommand.ID:
                    return "OreRefinementUpdateCommand";
                case QuestListUpdateCommand.ID:
                    return "QuestListUpdateCommand";
                case QuestUpdateCommand.ID:
                    return "QuestUpdateCommand";
                case QuestDetailsUpdateCommand.ID:
                    return "QuestDetailsUpdateCommand";
            }

            if (packetId == CreatePortalCommand.ID)
                return "CreatePortalCommand";
            if (packetId == RemovePortalCommand.ID)
                return "RemovePortalCommand";
            if (packetId == ActivatePortalCommand.ID)
                return "ActivatePortalCommand";

            return null;
        }

        private static void Notify(Player player, string direction, string packetName, short packetId)
        {
            if (player == null)
                return;

            var mapId = player.Spacemap == null ? -1 : player.Spacemap.Id;
            player.SendPacket($"0|A|STD|[PACKET_DEBUG] {direction} {packetName} ID={packetId} MAP={mapId}");
        }
    }
}
