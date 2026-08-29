using Ow.Game.Objects;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests;
using Ow.Utils;
using System;

namespace Ow.Net.netty
{
    static class PacketDebug
    {
        // Toggle this flag to enable or disable every packet-debug trace.
        // It is enabled by default so the current diagnostics keep working.
        public static bool Enabled = false;

        public static void NotifyIncoming(Player player, string packetName, short packetId)
        {
            Notify(player, "C2S", packetName, packetId);
        }

        public static void NotifyPortalCount(Player player, int mapId, int portalCount)
        {
            if (!Enabled || player == null)
                return;

            player.SendPacket($"0|A|STD|[PACKET_DEBUG] MAP_PORTALS MAP={mapId} COUNT={portalCount}");
        }

        public static void NotifyOutgoing(Player player, byte[] command)
        {
            if (!Enabled || command == null || command.Length < 4)
                return;

            var packetId = (short)((command[2] << 8) | (command[3] & 0xff));
            var packetName = GetOutgoingPacketName(packetId);
            if (packetName != null)
                Notify(player, "S2C", packetName, packetId, command.Length);
        }

        public static void NotifyLegacyOutgoing(Player player, string packet)
        {
            const string configurationPrefix = "0|S|CFG|";
            if (!Enabled || string.IsNullOrEmpty(packet) || !packet.StartsWith(configurationPrefix, StringComparison.Ordinal))
                return;

            NotifyText(player, "S2C Configuration CFG=" + packet.Substring(configurationPrefix.Length));
        }

        public static void NotifyException(Player player, short packetId, int packetLength, Exception exception)
        {
            if (!Enabled)
                return;

            var packetName = GetIncomingPacketName(packetId) ?? "UnknownPacket";
            var playerId = player == null ? -1 : player.Id;
            var mapId = player == null || player.Spacemap == null ? -1 : player.Spacemap.Id;
            var message = $"- [PacketDebug] C2S {packetName} ID={packetId} LENGTH={packetLength} PLAYER={playerId} MAP={mapId} EXCEPTION={exception}";

            Logger.Log("packet_debug", message);
            Logger.Log("error_log", message);
        }

        private static string GetOutgoingPacketName(short packetId)
        {
            if (packetId == AssetCreateCommand.ID)
                return "AssetCreateCommand";

            switch (packetId)
            {
                case ShipInitializationCommand.ID:
                    return "ShipInitializationCommand";
                case ShipCreateCommand.ID:
                    return "ShipCreateCommand";
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
                case MapAddPOICommand.ID:
                    return "MapAddPOICommand";
                case SectorControlBeaconProgressVisibilityCommand.ID:
                    return "SectorControlBeaconProgressVisibilityCommand";
                case SectorControlBeaconUpdateCommand.ID:
                    return "SectorControlBeaconUpdateCommand";
            }

            if (packetId == CreatePortalCommand.ID)
                return "CreatePortalCommand";
            if (packetId == RemovePortalCommand.ID)
                return "RemovePortalCommand";
            if (packetId == ActivatePortalCommand.ID)
                return "ActivatePortalCommand";
            if (packetId == SetSpeedCommand.ID)
                return "SetSpeedCommand";

            return null;
        }

        private static string GetIncomingPacketName(short packetId)
        {
            switch (packetId)
            {
                case CollectOreRequest.ID: return "CollectOreRequest";
                case SellOreRequest.ID: return "SellOreRequest";
                case TradeOreRequest.ID: return "TradeOreRequest";
                case RefineOreRequest.ID: return "RefineOreRequest";
                case SelectMenuBarItemRequest.ID: return "SelectMenuBarItemRequest";
                case UIOpenRequest.ID: return "UIOpenRequest";
                case ProActionBarRequest.ID: return "ProActionBarRequest";
                case AbortQuestRequest.ID: return "AbortQuestRequest";
                case AcceptQuestRequest.ID: return "AcceptQuestRequest";
                case QuestDetailsRequest.ID: return "QuestDetailsRequest";
                case QuestFiltersRequest.ID: return "QuestFiltersRequest";
                case QuestGiverRequest.ID: return "QuestGiverRequest";
                case QuestListRequest.ID: return "QuestListRequest";
                case QuestWindowCloseRequest.ID: return "QuestWindowCloseRequest";
                default: return null;
            }
        }

        private static void Notify(Player player, string direction, string packetName, short packetId, int packetLength = -1)
        {
            if (!Enabled || player == null)
                return;

            var mapId = player.Spacemap == null ? -1 : player.Spacemap.Id;
            var length = packetLength >= 0 ? $" LENGTH={packetLength}" : string.Empty;
            player.SendPacket($"0|A|STD|[PACKET_DEBUG] {direction} {packetName} ID={packetId}{length} MAP={mapId}");
        }

        private static void NotifyText(Player player, string message)
        {
            if (!Enabled || player == null)
                return;

            var mapId = player.Spacemap == null ? -1 : player.Spacemap.Id;
            player.SendPacket($"0|A|STD|[PACKET_DEBUG] {message} MAP={mapId}");
        }

        public static void Log(string category, string message)
        {
            if (!Enabled)
                return;

            Logger.Log(category, message);
        }
    }
}
