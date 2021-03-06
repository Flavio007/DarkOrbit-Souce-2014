using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ow.Utils;
using Ow.Game.Movements;
using Ow.Game;

namespace Ow.Game.Objects.Stations
{
    class OreTradeStation : Activatable
    {
        public OreTradeStation(Spacemap spacemap, int factionId, Position position, Clan clan) : base(spacemap, factionId, position, clan, AssetTypeModule.ORE_TRADE_STATION) { }

        public override void Click(GameSession gameSession) { }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            return AssetCreateCommand.write(GetAssetType(), "OreTrade",
                                          FactionId, "", Id, 0, 0,
                                          Position.X, Position.Y, 0, true, true, true, false,
                                          new ClanRelationModule(clanRelationModule),
                                          new List<VisualModifierCommand>());
        }
    }
}
