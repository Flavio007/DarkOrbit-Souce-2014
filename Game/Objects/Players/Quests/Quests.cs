using Ow.Game.Objects.Players.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ow.Game.Objects;

namespace Ow.Game.Objects.Players
{
    class Quests
    {
        public Player Player { get; set; }

        public const string QuestName = "";

        public int QuestState = 0;

        public bool CanDie = true;

        public string LoadLootId { get; set; }

        public bool ReloadingActive = false;

        public Quests(Player player) { Player = player; LoadLootId = AmmunitionManager.ECO_10; }

        public void Tick()
        {

        }


        class killNpc
        {
            public int ShipId;
            public int Count;
            public int MapId;
        }

        class KillPlayer
        {
            public int PlayerShipId;
            public int PlayerFaction;
            public int Count;
        }

        class KillPlayerAny
        {
            public int Count;
            public int Faction;
        }

        class CollectOre
        {
            public int OreId;
            public int OreCount;
        }

        class CollectBox
        {
            public int BoxId;
            public int BoxCount;
        }

        class MoveTo
        {
            public int PosX;
            public int PosY;
            public int MapId;
        }
    }
}
