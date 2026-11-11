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
    class QuestGiverStation : Activatable
    {
        public bool EmbeddedInHomeStation { get; set; }

        public QuestGiverStation(Spacemap spacemap, int factionId, Position position, Clan clan) : base(spacemap, factionId, position, clan, AssetTypeModule.QUESTGIVER) { }

        public override void Click(GameSession gameSession)
        {
            gameSession?.Player?.Quests?.OpenQuestGiver(Id);
        }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            var design = FactionId == 2 ? 6 : FactionId == 3 ? 9 : 3;
            var name = FactionId == 2 ? "Reginald Crowley" : FactionId == 3 ? "Vanessa Arkadium" : "Morgus Petterson";
            return AssetCreateCommand.write(GetAssetType(), EmbeddedInHomeStation ? "__home_service_quest" : name,
                                          FactionId, "", Id, design, 0,
                                          Position.X, Position.Y, 0, true, true, true, true,
                                          new ClanRelationModule(clanRelationModule),
                                          new List<VisualModifierCommand>());
        }
    }
}
