using Ow.Game.Objects;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests;
using Ow.Utils;
using System;
using System.Globalization;
using System.Text;

namespace Ow.Net.netty
{
    static class PacketDebug
    {
        // Controls the in-game diagnostic messages and the existing packet traces.
        public static bool Enabled = false;

        // Captures the raw upgrade request without enabling diagnostics for every
        // other packet. Turn this off after the upgrade issue is diagnosed.
        public static bool RawIncomingEnabled = true;
        public static bool RawOreRefinementOutgoingEnabled = true;

        public static void NotifyIncoming(Player player, string packetName, short packetId)
        {
            Notify(player, "C2S", packetName, packetId);
        }

        public static void NotifyIncoming(Player player, string packetName, short packetId, byte[] packet)
        {
            NotifyIncoming(player, packetName, packetId);
            LogRawIncoming(player, packetName, packetId, packet);
        }

        public static void NotifyRefineOreDecoded(Player player, RefineOreRequest request, byte[] packet)
        {
            if (!RawIncomingEnabled || request == null)
                return;

            var playerId = player == null ? -1 : player.Id;
            var mapId = player == null || player.Spacemap == null ? -1 : player.Spacemap.Id;
            var sourceType = request.Source == null ? "<null>" : request.Source.TypeValue.ToString(CultureInfo.InvariantCulture);
            var targetCount = request.Target == null
                ? "<null>"
                : request.Target.Count.ToString("R", CultureInfo.InvariantCulture);
            var targetResourceType = request.Target == null || request.Target.Resource == null
                ? "<null>"
                : request.Target.Resource.TypeValue.ToString(CultureInfo.InvariantCulture);
            var length = packet == null ? 0 : packet.Length;
            var message = $"- [PacketDebug] C2S RefineOreRequest ID={RefineOreRequest.ID} LENGTH={length} PLAYER={playerId} MAP={mapId}" +
                          $" TARGET_MARKER={request.TargetFieldMarker} TARGET_ID={OreStackCommand.ID}" +
                          $" TARGET_COUNT={targetCount} TARGET_RESOURCE_ID={OreResourceTypeModule.ID} TARGET_RESOURCE_TYPE={targetResourceType}" +
                          $" SOURCE_MARKER={request.SourceFieldMarker} SOURCE_ID={RefinementTypeModule.ID} SOURCE_TYPE={sourceType}" +
                          $" MARKERS_OK={request.HasExpectedFieldMarkers} CONSUMED={request.BytesConsumed} HEX={ToHex(packet)}";

            Logger.Log("packet_debug", message);
            Out.WriteLine(message, "PacketDebug", ConsoleColor.DarkYellow);
        }

        public static void NotifyPortalCount(Player player, int mapId, int portalCount)
        {
            if (!Enabled || player == null)
                return;

            player.SendPacket($"0|A|STD|[PACKET_DEBUG] MAP_PORTALS MAP={mapId} COUNT={portalCount}");
        }

        public static void NotifyOutgoing(Player player, byte[] command)
        {
            if (command == null || command.Length < 4)
                return;

            var packetId = (short)((command[2] << 8) | (command[3] & 0xff));
            if (packetId == OreRefinementUpdateCommand.ID)
                LogRawOutgoing(player, "OreRefinementUpdateCommand", packetId, command);

            if (!Enabled)
                return;

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
                case CameraLockToCoordinatesCommand.ID:
                    return "CameraLockToCoordinatesCommand";
                case CameraLockToShipCommand.ID:
                    return "CameraLockToShipCommand";
                case CameraLockToHeroCommand.ID:
                    return "CameraLockToHeroCommand";
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

        private static void LogRawIncoming(Player player, string packetName, short packetId, byte[] packet)
        {
            if (!RawIncomingEnabled)
                return;

            var playerId = player == null ? -1 : player.Id;
            var mapId = player == null || player.Spacemap == null ? -1 : player.Spacemap.Id;
            var length = packet == null ? 0 : packet.Length;
            var message = $"- [PacketDebug] C2S {packetName} ID={packetId} LENGTH={length} PLAYER={playerId} MAP={mapId} HEX={ToHex(packet)}";

            Logger.Log("packet_debug", message);
            Out.WriteLine(message, "PacketDebug", ConsoleColor.DarkYellow);
        }

        private static void LogRawOutgoing(Player player, string packetName, short packetId, byte[] packet)
        {
            if (!RawOreRefinementOutgoingEnabled)
                return;

            var playerId = player == null ? -1 : player.Id;
            var mapId = player == null || player.Spacemap == null ? -1 : player.Spacemap.Id;
            var length = packet == null ? 0 : packet.Length;
            var message = $"- [PacketDebug] S2C {packetName} ID={packetId} LENGTH={length} PLAYER={playerId} MAP={mapId} HEX={ToHex(packet)}";

            Logger.Log("packet_debug", message);
            Out.WriteLine(message, "PacketDebug", ConsoleColor.DarkCyan);
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var builder = new StringBuilder(bytes.Length * 3);
            for (var i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                    builder.Append(' ');

                builder.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
