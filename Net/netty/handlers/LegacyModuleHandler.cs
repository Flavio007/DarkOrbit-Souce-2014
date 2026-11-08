using Ow.Game;
using Ow.Managers;
using Ow.Game.Movements;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Net.netty.handlers
{
    class LegacyModuleHandler : IHandler
    {
        private static bool TryParseOre(string token, out Ores oreType)
        {
            oreType = Ores.DUMMY;
            if (string.IsNullOrWhiteSpace(token)) return false;

            token = token.Trim().ToLowerInvariant();
            switch (token)
            {
                case "1":
                case "prometium":
                case "ore_prometium":
                    oreType = Ores.Prometium;
                    return true;
                case "2":
                case "endurium":
                case "ore_endurium":
                    oreType = Ores.Endurium;
                    return true;
                case "3":
                case "terbium":
                case "ore_terbium":
                    oreType = Ores.Terbium;
                    return true;
                case "4":
                case "xenomit":
                case "ore_xenomit":
                    oreType = Ores.Xenomit;
                    return true;
                case "5":
                case "prometid":
                case "ore_prometid":
                    oreType = Ores.Prometid;
                    return true;
                case "6":
                case "duranium":
                case "ore_duranium":
                    oreType = Ores.Duranium;
                    return true;
                case "7":
                case "promerium":
                case "ore_promerium":
                    oreType = Ores.Promerium;
                    return true;
                case "9":
                case "palladium":
                case "ore_palladium":
                    oreType = Ores.Palladium;
                    return true;
                default:
                    return false;
            }
        }

        private static int ParseAmount(string[] packet, int defaultValue = 1)
        {
            for (var i = packet.Length - 1; i >= 0; i--)
                if (int.TryParse(packet[i], out var value) && value > 0)
                    return value;

            return defaultValue;
        }

        private static bool TryResolveOreFromPacket(string[] packet, out Ores oreType)
        {
            oreType = Ores.DUMMY;
            foreach (var part in packet)
                if (TryParseOre(part, out oreType))
                    return true;

            return false;
        }

        public void execute(GameSession gameSession, byte[] bytes)
        {
            var read = new LegacyModuleRequest();
            read.readCommand(bytes);

            var player = gameSession.Player;
            string[] packet = read.message.Split('|');
            if (packet.Length == 0 || string.IsNullOrWhiteSpace(packet[0]))
                return;
            if (player.ControlsLocked && packet[0] != "PNG") return;

            switch (packet[0])
            {
                case ServerCommands.QUESTFM_INFO:
                    if (packet.Length == 3 && packet[1] == ServerCommands.QUESTFM_PRIVILEGE_QUEST &&
                        int.TryParse(packet[2], out var highlightedQuestId))
                        player.Quests?.HighlightAcceptedQuest(highlightedQuestId);
                    break;
                  case "BUY":
                      if(packet.Length==2)player.Achievements?.QuickBuy(packet[1]);
                      break;
                  case "PNG":
                    if (packet.Length == 2 && packet[1] == "WASM")
                        player.SendPacket("0|A|WASM_PONG|" + Ow.Game.Ticks.TickManager.CurrentTickId);
                    break;
                case "CLI":
                    // The client is untrusted: authorize each command on the authenticated player.
                    if (player.RankId != 21) return;
                    if (packet.Length != 4 || packet[1].ToUpperInvariant() != "SET" || !int.TryParse(packet[3], out var value) || value < 0)
                    {
                        player.SendPacket("0|CLI|Uso: SET MAP/HP/SHD/SHIP valor");
                        break;
                    }
                    switch (packet[2].ToUpperInvariant())
                    {
                        case "HP":
                            player.CurrentHitPoints = Math.Min(value, player.MaxHitPoints);
                            player.UpdateStatus();
                            player.SendPacket($"0|CLI|HP atualizado: {player.CurrentHitPoints}");
                            break;
                        case "SHD":
                            player.CurrentShieldPoints = Math.Min(value, player.MaxShieldPoints);
                            player.UpdateStatus();
                            player.SendPacket($"0|CLI|Escudo atualizado: {player.CurrentShieldPoints}");
                            break;
                        case "SHIP":
                            if (GameManager.GetShip(value) == null) { player.SendPacket("0|CLI|Nave inexistente"); break; }
                            player.ChangeShip(value);
                            player.SendPacket($"0|CLI|Nave atualizada: {value}");
                            break;
                        case "MAP":
                            var map = GameManager.GetSpacemap(value);
                            if (map == null || player.Storage.Jumping) { player.SendPacket("0|CLI|Mapa indisponivel"); break; }
                            player.SendPacket($"0|CLI|Saltando para o mapa {value}");
                            player.Jump(value, new Position(map.Limits[1].X / 2, map.Limits[1].Y / 2));
                            break;
                        default:
                            player.SendPacket("0|CLI|Comando desconhecido");
                            break;
                    }
                    break;
                case ServerCommands.SET_STATUS:
                    if (packet.Length < 3) break;
                    switch (packet[1])
                    {
                        case ClientCommands.CONFIGURATION:
                            player.ChangeConfiguration(packet[2]);
                            break;
                    }
                    break;
                case ServerCommands.ACHIEVEMENTS:
                    if (packet.Length > 2 && packet[1] == ServerCommands.ACHIEVEMENT_BUY)
                    {
                        int achievementId;
                        if (int.TryParse(packet[2], out achievementId))
                            player.Achievements?.HandleBuyRequest(achievementId);
                    }
                    break;
                case ServerCommands.GET_ORE_PRICES:
                    player.SendOreShopInfo();
                    break;
                case ServerCommands.SELL_ORE:
                    if (TryResolveOreFromPacket(packet, out var sellOre))
                    {
                        var sellAmount = ParseAmount(packet);
                        if (!player.TrySellOre(sellOre, sellAmount))
                            player.SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.SERVER_MSG}|Unable to sell ore.");
                    }
                    break;
                case ServerCommands.LAB:
                case ServerCommands.REFINEMENT:
                case ServerCommands.PRODUCE:
                case "refinement":
                case "refine":
                    if (TryResolveOreFromPacket(packet, out var refineOre))
                    {
                        var refineAmount = ParseAmount(packet);
                        if (!player.TryRefine(refineOre, refineAmount))
                            player.SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.SERVER_MSG}|Refinement failed: insufficient resources.");
                    }
                    else
                    {
                        player.SendOreShopInfo();
                    }
                    break;
            }
        }
    }
}
