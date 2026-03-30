using Ow.Game.Movements;
using Ow.Net.netty.commands;
using System.Collections.Generic;

namespace Ow.Game.Objects.Stations
{
    class HomeTurret : Asset
    {
        public int FactionId { get; }

        public HomeTurret(Spacemap spacemap, int factionId, Position position, short assetTypeId) : base(spacemap, position, assetTypeId)
        {
            FactionId = factionId;
        }

        public override byte[] GetAssetCreateCommand()
        {
            return AssetCreateCommand.write(new AssetTypeModule(AssetTypeId), "Turret",
                              FactionId, "", Id, 0, 0,
                              Position.X, Position.Y, 0, true, true, true, false,
                              new ClanRelationModule(ClanRelationModule.NONE),
                              new List<VisualModifierCommand>());
        }
    }
}