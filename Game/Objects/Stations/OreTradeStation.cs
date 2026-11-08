using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ow.Utils;
using Ow.Game.Movements;
using Ow.Game;
using Ow.Net.netty;

namespace Ow.Game.Objects.Stations
{
    class OreTradeStation : Activatable
    {
        public bool EmbeddedInHomeStation { get; set; }
        public BattleStation FactionStation { get; set; }
        public override int FactionId { get => FactionStation != null ? FactionStation.FactionId : base.FactionId; set => base.FactionId=value; }
        public bool CanTrade(Player player) => FactionStation != null ? FactionStation.CanTradeOre(player)
            : player != null && !player.Destroyed && player.FactionId == FactionId && player.Spacemap == Spacemap && player.Position.DistanceTo(Position) <= 700;

        public OreTradeStation(Spacemap spacemap, int factionId, Position position, Clan clan) : base(spacemap, factionId, position, clan, AssetTypeModule.ORE_TRADE_STATION) { }

        public override void Click(GameSession gameSession)
        {
            var player = gameSession.Player;
            if (player == null) return;
            if (!CanTrade(player)) return;

            player.SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.TRADE_WINDOW_ACTIVATION}|1");
            player.SendOreShopInfo();
        }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            return AssetCreateCommand.write(GetAssetType(), EmbeddedInHomeStation ? "__home_service_trade" : "OreTrade",
                                          FactionId, "", Id, 0, 0,
                                          Position.X, Position.Y, 0, true, true, true, true,
                                          new ClanRelationModule(clanRelationModule),
                                          new List<VisualModifierCommand>());
        }
    }
}
