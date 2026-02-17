using Ow.Game;
using Ow.Game.Movements;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects.Stations
{
    class StationBase
    {
        public int TypeId { get; set; }
        public int FactionId { get; set; }
        public List<int> Position { get; set; }
    }

    class HomeStation : Activatable
    {
        public static int SECURE_ZONE_RANGE = 1500;
        private static readonly int[] SolitaryHomeMaps = { 20, 24, 28 };

        public RepairStation RepairStation { get; set; }
        public HangarStation HangarStation { get; set; }
        public QuestGiverStation QuestGiverStation { get; set; }
        public OreTradeStation OreTradeStation { get; set; }

        public HomeStation(Spacemap spacemap, int factionId, Position position, Clan clan) : base(spacemap, factionId, position, clan, AssetTypeModule.BASE_COMPANY)
        {
            if (!UseSolitaryVisual())
                PrepareStations();
        }

        private bool UseSolitaryVisual()
        {
            return SolitaryHomeMaps.Contains(Spacemap.Id);
        }

        private int GetVisualFactionId()
        {
            return UseSolitaryVisual() ? FactionId + 3 : FactionId;
        }

        private AssetTypeModule GetVisualAssetType()
        {
            return new AssetTypeModule(UseSolitaryVisual() ? AssetTypeModule.HANGAR_HOME : AssetTypeId);
        }

        public void PrepareStations()
        {
            var rPosition = new Position(Position.X + 0, Position.Y + -1080);
            RepairStation = new RepairStation(Spacemap, FactionId, rPosition, Clan);

            var hPosition = new Position(Position.X + +1080, Position.Y + 0);
            HangarStation = new HangarStation(Spacemap, FactionId, hPosition, Clan);

            var qPosition = new Position(Position.X + 0, Position.Y + 1080);
            QuestGiverStation = new QuestGiverStation(Spacemap, FactionId, qPosition, Clan);

            var oPosition = new Position(Position.X + -1080, Position.Y + -1);
            OreTradeStation = new OreTradeStation(Spacemap, FactionId, oPosition, Clan);
        }

        public override void Click(GameSession gameSession)
        {

        }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            return AssetCreateCommand.write(GetVisualAssetType(), "HQ",
                                          GetVisualFactionId(), "", Id, 0, 0,
                                          Position.X, Position.Y, 0, true, true, true, false,
                                          new ClanRelationModule(clanRelationModule),
                                          new List<VisualModifierCommand>());
        }
    }
}
