using Ow.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game
{
    class ClanBattleStationInventoryItem
    {
        public int ItemId { get; set; }
        public short Type { get; set; }
        public int UpgradeLevel { get; set; }
        public bool InUse { get; set; }

        public ClanBattleStationInventoryItem()
        {
        }

        public ClanBattleStationInventoryItem(int itemId, short type, int upgradeLevel, bool inUse)
        {
            ItemId = itemId;
            Type = type;
            UpgradeLevel = upgradeLevel;
            InUse = inUse;
        }
    }

    class Clan
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public int FactionId { get; set; }
        public int LeaderId { get; set; }

        public Dictionary<int, Diplomacy> Diplomacies = new Dictionary<int, Diplomacy>();
        public List<ClanBattleStationInventoryItem> BattleStationInventory = new List<ClanBattleStationInventoryItem>();

        public Clan(int id, string name, string tag, int factionId, int leaderId = 0)
        {
            Id = id;
            Name = name;
            Tag = tag;
            FactionId = factionId;
            LeaderId = leaderId;
        }

        public short GetRelation(Clan clan)
        {
            if (clan == this && clan.Id != 0)
                return (short)Game.Diplomacy.ALLIED;
            if (Diplomacies.ContainsKey(clan.Id))
                return (short)Diplomacies[clan.Id];
            return (short)Game.Diplomacy.NONE;
        }
    }
}
