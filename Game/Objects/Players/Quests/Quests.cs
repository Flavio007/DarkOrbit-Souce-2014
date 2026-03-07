using Ow.Game.Objects.Players.Managers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ow.Game.Objects;
using Ow.Net.netty;
using Ow.Net.netty.commands;

namespace Ow.Game.Objects.Players
{
    class Quests
    {
        public const int QUEST_ID_PARTING_ADVICE = 1;
        public const int QUEST_STATE_AVAILABLE = 0;
        public const int QUEST_STATE_ACTIVE = 1;
        public const int QUEST_STATE_COMPLETED = 2;
        private const int QUEST_SYSTEM_TYPE_STANDARD = 0;

        public Player Player { get; set; }

        public const string QuestName = "Parting Advice";

        public int QuestState = 0;
        public int ActiveQuestId = 0;

        public bool CanDie = true;

        public string LoadLootId { get; set; }

        public bool ReloadingActive = false;

        public Quests(Player player)
        {
            Player = player;
            LoadLootId = AmmunitionManager.ROCKET_LAUNCHER_ECO_10;
            EnsureQuest1Accepted();
        }

        public void Tick()
        {
            if (ActiveQuestId == QUEST_ID_PARTING_ADVICE && QuestState == QUEST_STATE_ACTIVE && IsPlayerInClan())
                CompleteQuest1();
        }

        public void OpenWindow()
        {
            SendQuestSystemInit();
            SendQuestSystemUpdate();
        }

        public void HandleQuestGiverClick()
        {
            OpenWindow();
        }

        public void Sync()
        {
            EnsureQuest1Accepted();
            SendQuestSystemInit();
            SendQuestSystemUpdate();
        }

        public void TryCompleteClanQuest()
        {
            if (ActiveQuestId == QUEST_ID_PARTING_ADVICE && QuestState == QUEST_STATE_ACTIVE && IsPlayerInClan())
                CompleteQuest1();
        }

        private void EnsureQuest1Accepted()
        {
            if (ActiveQuestId == QUEST_ID_PARTING_ADVICE && QuestState != QUEST_STATE_AVAILABLE)
                return;

            ActiveQuestId = QUEST_ID_PARTING_ADVICE;
            QuestState = QUEST_STATE_ACTIVE;
            TryCompleteClanQuest();
        }

        private void CompleteQuest1()
        {
            if (QuestState == QUEST_STATE_COMPLETED)
                return;

            QuestState = QUEST_STATE_COMPLETED;
            SendQuestSystemUpdate();

            var replacements = new List<MessageWildcardReplacementModule>
            {
                new MessageWildcardReplacementModule(
                    "%quest_name%",
                    QuestName,
                    new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.PLAIN))
            };

            Player.SendCommand(new MessageLocalizedWildcardCommand(
                "q2_accepted_quest",
                new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED),
                replacements).write());
            Player.SendPacket($"0|A|STD|[Quest] Completed quest {QUEST_ID_PARTING_ADVICE}: {QuestName}");
        }

        private void SendQuestSystemInit()
        {
            Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_INIT}|{BuildQuestSystemXml()}|{QUEST_SYSTEM_TYPE_STANDARD}");
            Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_HIGHLIGHT_QUEST}|{QUEST_ID_PARTING_ADVICE}");
        }

        private void SendQuestSystemUpdate()
        {
            Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_UPDATE}|{BuildQuestSystemXml()}|{QUEST_SYSTEM_TYPE_STANDARD}");
            Player.SendPacket($"0|{ServerCommands.QUEST_INFO}|{ServerCommands.QUEST_STATUS}|{QUEST_ID_PARTING_ADVICE}|{GetLegacyQuestStatus()}|{GetQuestProgressValue()}|1");
        }

        private bool IsPlayerInClan()
        {
            return Player?.Clan != null && Player.Clan.Id != 0;
        }

        private int GetLegacyQuestStatus()
        {
            switch (QuestState)
            {
                case QUEST_STATE_COMPLETED:
                    return 2;
                case QUEST_STATE_ACTIVE:
                    return 1;
                default:
                    return 0;
            }
        }

        private int GetQuestProgressValue()
        {
            return IsPlayerInClan() ? 1 : 0;
        }

        private string BuildQuestSystemXml()
        {
            var factionSuffix = GetFactionSuffix();
            var status = GetQuestStateName();
            var progress = GetQuestProgressValue();

            return string.Format(
                CultureInfo.InvariantCulture,
                "<quests><quest id=\"{0}\" status=\"{1}\" accepted=\"1\" visible=\"1\" activate=\"1\" accomplished=\"{2}\" claimable=\"{2}\" sortOrder=\"1\" level=\"1\" titleKey=\"quest_title_{0}\" descriptionKey=\"quest_description_{0}_{3}\"><conditions><condition id=\"join_clan\" type=\"joinClan\" current=\"{4}\" target=\"1\" descriptionKey=\"q2_condition_JOIN_CLAN\" /></conditions></quest></quests>",
                QUEST_ID_PARTING_ADVICE,
                status,
                QuestState == QUEST_STATE_COMPLETED ? 1 : 0,
                factionSuffix,
                progress);
        }

        private string GetQuestStateName()
        {
            switch (QuestState)
            {
                case QUEST_STATE_COMPLETED:
                    return "completed";
                case QUEST_STATE_ACTIVE:
                    return "accepted";
                default:
                    return "available";
            }
        }

        private string GetFactionSuffix()
        {
            switch (Player?.FactionId ?? 1)
            {
                case 2:
                    return "eic";
                case 3:
                    return "vru";
                default:
                    return "mmo";
            }
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

        class Restrisctions
        {
            public bool NoDeath;
            public int TimeLimit; // In seconds
            public bool NoShooting;
        }
    }
}

