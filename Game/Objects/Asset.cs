using Ow.Game.Movements;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects
{
    class Asset : Object
    {
        public short AssetTypeId { get; set; }
        public string VisualName { get; set; } = "";
        public int FactionId { get; set; }
        public string ClanTag { get; set; } = "";
        public int ClanId { get; set; }
        public int DesignId { get; set; }
        public int ExpansionStage { get; set; }
        public bool Invisible { get; set; }
        public bool VisibleOnWarnRadar { get; set; }
        public bool DetectedByWarnRadar { get; set; }
        public bool ShowBubble { get; set; }

        public Asset(Spacemap spacemap, Position position, short assetTypeId) : base(Randoms.CreateRandomID(), position, spacemap)
        {
            AssetTypeId = assetTypeId;
        }

        public Asset(int id, Spacemap spacemap, Position position, short assetTypeId) : base(id, position, spacemap)
        {
            AssetTypeId = assetTypeId;
        }

        public virtual byte[] GetAssetCreateCommand()
        {
            return AssetCreateCommand.write(new AssetTypeModule(AssetTypeId), VisualName,
                              FactionId, ClanTag, Id, DesignId, ExpansionStage,
                              Position.X, Position.Y, ClanId, Invisible, VisibleOnWarnRadar, DetectedByWarnRadar, ShowBubble,
                              new ClanRelationModule(ClanRelationModule.NONE),
                              new List<VisualModifierCommand>());
        }

        public void Remove()
        {
            Spacemap.Objects.TryRemove(Id, out var asset);
            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(new AssetTypeModule(AssetTypeId), Id));
        }
    }
}
