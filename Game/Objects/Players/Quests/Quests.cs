using Ow.Game.Objects.Players.Managers;
using Ow.Game.Objects.Stations;
using Ow.Managers;
using Ow.Net.netty;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Ow.Game.Objects.Players
{
    internal sealed class QuestPlayerState
    {
        public int QuestId { get; set; }
        public int State { get; set; }
        public Dictionary<string, int> Progress { get; set; }

        public QuestPlayerState(int questId)
        {
            QuestId = questId;
            State = Quests.QUEST_STATE_AVAILABLE;
            Progress = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal sealed class QuestConditionDefinition
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string DescriptionKey { get; set; }
        public int Target { get; set; }
        public int WireId { get; set; }
        public uint WireType { get; set; }
    }

    internal sealed class QuestRewardDefinition
    {
        public string LootId { get; set; }
        public int Amount { get; set; }
    }

    internal sealed class QuestDefinition
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string TitleKey { get; set; }
        public string Description { get; set; }
        public string DescriptionKey { get; set; }
        public int SortOrder { get; set; }
        public int MinLevel { get; set; }
        public int Priority { get; set; }
        public short Icon { get; set; }
        public int FactionId { get; set; }
        public List<QuestConditionDefinition> Conditions { get; private set; }
        public List<QuestRewardDefinition> Rewards { get; private set; }

        public QuestDefinition()
        {
            Conditions = new List<QuestConditionDefinition>();
            Rewards = new List<QuestRewardDefinition>();
        }
    }

    internal static class QuestCatalog
    {
        private const string ConfigFile = "config\\quests.xml";
        private static readonly object SyncRoot = new object();
        private static Dictionary<int, QuestDefinition> definitions;

        public static List<QuestDefinition> All()
        {
            EnsureLoaded();
            lock (SyncRoot)
                return definitions.Values.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
        }

        public static QuestDefinition Get(int id)
        {
            EnsureLoaded();
            lock (SyncRoot)
            {
                QuestDefinition result;
                return definitions.TryGetValue(id, out result) ? result : null;
            }
        }

        private static void EnsureLoaded()
        {
            if (definitions != null)
                return;

            lock (SyncRoot)
            {
                if (definitions != null)
                    return;
                definitions = Load();
                if (!definitions.ContainsKey(Quests.QUEST_ID_PARTING_ADVICE))
                    definitions[Quests.QUEST_ID_PARTING_ADVICE] = Seed();
            }
        }

        private static Dictionary<int, QuestDefinition> Load()
        {
            var result = new Dictionary<int, QuestDefinition>();
            var path = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFile),
                Path.Combine(Directory.GetCurrentDirectory(), ConfigFile)
            }.FirstOrDefault(File.Exists);

            try
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var root = XDocument.Load(path).Root;
                    if (root != null)
                    {
                        foreach (var e in root.Elements().Where(x => string.Equals(x.Name.LocalName, "quest", StringComparison.OrdinalIgnoreCase)))
                        {
                            int id;
                            if (!TryInt(e, "id", out id) || id <= 0)
                                continue;

                            var q = new QuestDefinition
                            {
                                Id = id,
                                Title = Value(e, "title", Value(e, "name", "Quest " + id)),
                                TitleKey = Value(e, "titleKey", "quest_title_" + id),
                                Description = Value(e, "description", ""),
                                DescriptionKey = Value(e, "descriptionKey", "quest_description_" + id),
                                SortOrder = Int(e, "sortOrder", id),
                                MinLevel = Math.Max(1, Int(e, "minLevel", Int(e, "level", 1))),
                                Priority = Int(e, "priority", 0),
                                Icon = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Int(e, "icon", 0))),
                                FactionId = Int(e, "factionId", 0)
                            };
                            var conditions = e.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "conditions", StringComparison.OrdinalIgnoreCase));
                            if (conditions != null)
                            {
                                var index = 0;
                                foreach (var c in conditions.Elements().Where(x => string.Equals(x.Name.LocalName, "condition", StringComparison.OrdinalIgnoreCase)))
                                {
                                    index++;
                                    var conditionId = Value(c, "id", "condition_" + index);
                                    q.Conditions.Add(new QuestConditionDefinition
                                    {
                                        Id = conditionId,
                                        Type = Value(c, "type", conditionId),
                                        DescriptionKey = Value(c, "descriptionKey", ""),
                                        Target = Math.Max(1, Int(c, "target", Int(c, "amount", 1))),
                                        WireId = Math.Max(1, Int(c, "wireId", index)),
                                        WireType = (uint)Math.Max(0, Int(c, "wireType", ResolveWireType(Value(c, "type", conditionId))))
                                    });
                                }
                            }
                            var rewards = e.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "rewards", StringComparison.OrdinalIgnoreCase));
                            if (rewards != null)
                                foreach (var r in rewards.Elements().Where(x => string.Equals(x.Name.LocalName, "reward", StringComparison.OrdinalIgnoreCase)))
                                    q.Rewards.Add(new QuestRewardDefinition { LootId = Value(r, "lootId", Value(r, "id", "")), Amount = Math.Max(0, Int(r, "amount", 0)) });
                            result[id] = q;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [Quests.cs] Failed to load quest catalog: {e}");
            }

            if (result.Count == 0)
                result[Quests.QUEST_ID_PARTING_ADVICE] = Seed();
            return result;
        }

        private static QuestDefinition Seed()
        {
            var q = new QuestDefinition
            {
                Id = Quests.QUEST_ID_PARTING_ADVICE,
                Title = Quests.QuestName,
                TitleKey = "quest_title_1",
                Description = "Join a clan.",
                DescriptionKey = "quest_description_1_{faction}",
                SortOrder = 1,
                MinLevel = 1
            };
            q.Conditions.Add(new QuestConditionDefinition { Id = "join_clan", Type = "joinClan", DescriptionKey = "q2_condition_JOIN_CLAN", Target = 1, WireId = 1, WireType = 62 });
            return q;
        }

        private static int ResolveWireType(string type)
        {
            switch ((type ?? "").Trim().ToLowerInvariant())
            {
                case "joinclan":
                case "in_clan": return 62;
                case "killnpc": return 6;
                case "killnpcs": return 27;
                case "killplayers": return 28;
                case "collect": return 5;
                case "collectbonusbox": return 52;
                case "visitmap": return 31;
                case "jump": return 39;
                case "travel": return 13;
                default: return 0;
            }
        }

        private static string Value(XElement e, string name, string fallback)
        {
            var a = e.Attribute(name);
            if (a != null && !string.IsNullOrWhiteSpace(a.Value))
                return a.Value;
            var child = e.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
            return child != null && !string.IsNullOrWhiteSpace(child.Value) ? child.Value : fallback;
        }

        private static int Int(XElement e, string name, int fallback)
        {
            int result;
            return TryInt(e, name, out result) ? result : fallback;
        }

        private static bool TryInt(XElement e, string name, out int value)
        {
            return int.TryParse(Value(e, name, null), out value);
        }
    }

    class Quests
    {
        public const int QUEST_ID_PARTING_ADVICE = 1;
        public const int QUEST_STATE_AVAILABLE = 0;
        public const int QUEST_STATE_ACTIVE = 1;
        public const int QUEST_STATE_COMPLETED = 2;
        private const int QUEST_GIVER_RANGE = 700;

        public const string QuestName = "Parting Advice";
        public Player Player { get; set; }
        public int QuestState = QUEST_STATE_AVAILABLE;
        public int ActiveQuestId = 0;
        public int OpenQuestGiverId { get; private set; }
        public bool CanDie = true;
        public string LoadLootId { get; set; }
        public bool ReloadingActive = false;

        private const int MAX_NORMAL_QUEST_SLOTS = 5;
        private readonly Dictionary<int, QuestPlayerState> states = new Dictionary<int, QuestPlayerState>();
        private readonly HashSet<int> dirtyQuestIds = new HashSet<int>();
        private bool persistenceDirty;

        public Quests(Player player)
        {
            Player = player;
            LoadLootId = AmmunitionManager.ROCKET_LAUNCHER_ECO_10;
            InitializeCatalogStates();
        }

        public void Tick()
        {
            if (OpenQuestGiverId != 0 && !CanUseQuestGiver(OpenQuestGiverId))
                OpenQuestGiverId = 0;
            TryCompleteClanQuest();
        }

        public void OpenWindow()
        {
            SyncAcceptedTracker();
            // The Mission Control button opens the quest catalogue even when
            // the player is not currently interacting with a quest-giver.
            // Keep the station/range check for the station request itself,
            // but do not leave the general window with an empty list.
            SendModernQuestList(false);
        }

        public void HandleQuestGiverClick()
        {
            OpenWindow();
        }

        public bool OpenQuestGiver(int questGiverId)
        {
            if (!CanUseQuestGiver(questGiverId))
                return false;
            OpenQuestGiverId = questGiverId;
            SendModernQuestList();
            return true;
        }

        public void CloseQuestGiver(int questGiverId)
        {
            if (questGiverId == 0 || questGiverId == OpenQuestGiverId)
                OpenQuestGiverId = 0;
        }

        public bool CanUseQuestGiver(int questGiverId)
        {
            if (Player == null || Player.Spacemap == null || Player.Position == null || questGiverId <= 0)
                return false;
            var station = Player.Spacemap.GetActivatableMapEntity(questGiverId) as QuestGiverStation;
            return station != null && station.Position != null && Player.Position.DistanceTo(station.Position) <= QUEST_GIVER_RANGE && station.FactionId == Player.FactionId;
        }

        public void Sync()
        {
            SyncAcceptedTracker();
        }

        public void SyncAcceptedTracker()
        {
            var accepted = AcceptedStates();
            foreach (var state in accepted)
            {
                var definition = QuestCatalog.Get(state.QuestId);
                SendModernQuestUpdate(definition);
                SendConditionUpdates(definition, state);
            }

            var highlighted = accepted.FirstOrDefault();
            if (highlighted != null)
                Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_HIGHLIGHT_QUEST}|{highlighted.QuestId}");
        }

        public bool HandleAcceptQuest(int questId)
        {
            var definition = QuestCatalog.Get(questId);
            if (definition == null || !CanUseQuestGiver(OpenQuestGiverId) || !IsEligible(definition))
                return false;
            var state = GetState(questId);
            if (state.State == QUEST_STATE_COMPLETED || state.State == QUEST_STATE_ACTIVE)
                return true;

            state.State = QUEST_STATE_ACTIVE;
            bool completed;
            Evaluate(definition, state, out completed);
            PersistMutation(state);
            RefreshCompatibilityState();
            SyncAcceptedTracker();
            if (completed)
            {
                SendModernQuestUpdate(definition);
                SendConditionUpdates(definition, state);
            }
            SendModernQuestList();
            if (completed)
                SendQuestCompleted(definition);
            return true;
        }

        public bool HandleAbortQuest(int questId)
        {
            var state = GetStateIfKnown(questId);
            if (state == null || state.State != QUEST_STATE_ACTIVE)
                return true;
            state.State = QUEST_STATE_AVAILABLE;
            state.Progress.Clear();
            PersistMutation(state);
            RefreshCompatibilityState();
            SyncAcceptedTracker();
            Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_CANCEL_QUEST}|{questId}");
            if (CanUseQuestGiver(OpenQuestGiverId))
                SendModernQuestList();
            return true;
        }

        public void ApplyModernFilters(QuestFiltersRequest filters)
        {
            if (filters == null || Player == null || Player.Settings == null || Player.Settings.ClassY2T == null)
                return;
            var settings = Player.Settings.ClassY2T;
            settings.questsAvailableFilter = filters.QuestsAvailableFilter;
            settings.questsUnavailableFilter = filters.QuestsUnavailableFilter;
            settings.questsCompletedFilter = filters.QuestsCompletedFilter;
            settings.var_1151 = filters.ChallengesAttemptedFilter;
            settings.var_2239 = filters.ChallengesUnattemptedFilter;
            settings.questsLevelOrderDescending = filters.QuestsLevelOrderDescending;
            QueryManager.SavePlayer.Settings(Player, "classY2T", settings);
            Player.SettingsManager.SendUserSettingsCommand();
            // Filters are also available in the catalogue opened from the HUD.
            // That window has no quest-giver id, but still needs an immediate refresh.
            SendModernQuestList(false);
        }

        public void SendModernQuestDetails(int questId)
        {
            var definition = QuestCatalog.Get(questId);
            if (definition == null || !CanUseQuestGiver(OpenQuestGiverId))
                return;
            var wire = BuildModernDefinition(definition);
            Player.SendCommand(new QuestDetailsUpdateCommand(wire, null, QuestNettyModule.FromWire(QuestRatingModule.write())).write());
        }

        public void TryCompleteClanQuest()
        {
            foreach (var state in states.Values.Where(x => x.State == QUEST_STATE_ACTIVE).ToList())
            {
                var definition = QuestCatalog.Get(state.QuestId);
                if (definition == null)
                    continue;
                bool completed;
                if (!Evaluate(definition, state, out completed))
                    continue;
                PersistMutation(state);
                RefreshCompatibilityState();
                SyncAcceptedTracker();
                if (completed)
                {
                    SendModernQuestUpdate(definition);
                    SendConditionUpdates(definition, state);
                }
                if (completed)
                    SendQuestCompleted(definition);
            }
        }

        public bool ReportProgress(int questId, string conditionId, int currentValue)
        {
            var definition = QuestCatalog.Get(questId);
            var state = GetStateIfKnown(questId);
            if (definition == null || state == null || state.State != QUEST_STATE_ACTIVE || string.IsNullOrWhiteSpace(conditionId))
                return false;
            var condition = definition.Conditions.FirstOrDefault(x => string.Equals(x.Id, conditionId, StringComparison.OrdinalIgnoreCase));
            if (condition == null || string.Equals(condition.Type, "joinClan", StringComparison.OrdinalIgnoreCase))
                return false;
            var value = Math.Max(0, Math.Min(Math.Max(1, condition.Target), currentValue));
            int old;
            if (state.Progress.TryGetValue(condition.Id, out old) && old == value)
                return false;
            state.Progress[condition.Id] = value;
            bool completed;
            Evaluate(definition, state, out completed);
            PersistMutation(state);
            RefreshCompatibilityState();
            SyncAcceptedTracker();
            if (completed)
            {
                SendModernQuestUpdate(definition);
                SendConditionUpdates(definition, state);
            }
            if (completed)
                SendQuestCompleted(definition);
            return true;
        }

        public int ReportProgress(string conditionType, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(conditionType) || amount <= 0)
                return 0;

            var updates = new List<Tuple<int, string, int>>();
            foreach (var state in states.Values.Where(x => x.State == QUEST_STATE_ACTIVE))
            {
                var definition = QuestCatalog.Get(state.QuestId);
                if (definition == null)
                    continue;
                foreach (var condition in definition.Conditions.Where(x => string.Equals(x.Type, conditionType, StringComparison.OrdinalIgnoreCase)))
                {
                    var current = Current(condition, state);
                    updates.Add(Tuple.Create(definition.Id, condition.Id, current + amount));
                }
            }

            var changed = 0;
            foreach (var update in updates)
                if (ReportProgress(update.Item1, update.Item2, update.Item3))
                    changed++;
            return changed;
        }

        public void LoadPersistedState(DataTable rows)
        {
            InitializeCatalogStates();
            if (rows != null)
                foreach (DataRow row in rows.Rows)
                {
                    int questId;
                    if (!ReadInt(row, "quest_id", out questId) || QuestCatalog.Get(questId) == null)
                        continue;
                    var state = GetState(questId);
                    int value;
                    if (ReadInt(row, "state", out value))
                        state.State = Math.Max(QUEST_STATE_AVAILABLE, Math.Min(QUEST_STATE_COMPLETED, value));
                    var json = row.Table.Columns.Contains("progress_json") && row["progress_json"] != DBNull.Value ? Convert.ToString(row["progress_json"]) : "";
                    if (!string.IsNullOrWhiteSpace(json))
                        try
                        {
                            var progress = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                            if (progress != null)
                                state.Progress = new Dictionary<string, int>(progress, StringComparer.OrdinalIgnoreCase);
                        }
                        catch { state.Progress = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); }
                }
            persistenceDirty = false;
            dirtyQuestIds.Clear();
            RefreshCompatibilityState();
        }

        public void FlushPersistence()
        {
            if (!persistenceDirty)
                return;

            foreach (var questId in dirtyQuestIds.ToList())
            {
                QuestPlayerState state;
                if (!states.TryGetValue(questId, out state))
                    continue;
                if (QueryManager.SavePlayer.SaveQuestState(Player, state))
                    dirtyQuestIds.Remove(questId);
            }
            persistenceDirty = dirtyQuestIds.Count > 0;
        }

        private void InitializeCatalogStates()
        {
            states.Clear();
            foreach (var definition in QuestCatalog.All())
                states[definition.Id] = new QuestPlayerState(definition.Id);
            RefreshCompatibilityState();
        }

        private QuestPlayerState GetState(int id)
        {
            QuestPlayerState state;
            if (!states.TryGetValue(id, out state))
                states[id] = state = new QuestPlayerState(id);
            return state;
        }

        private QuestPlayerState GetStateIfKnown(int id)
        {
            QuestPlayerState state;
            return states.TryGetValue(id, out state) && QuestCatalog.Get(id) != null ? state : null;
        }

        private List<QuestPlayerState> AcceptedStates()
        {
            return states.Values.Where(x => x.State == QUEST_STATE_ACTIVE && QuestCatalog.Get(x.QuestId) != null).OrderBy(x => QuestCatalog.Get(x.QuestId).SortOrder).ThenBy(x => x.QuestId).ToList();
        }

        private bool Evaluate(QuestDefinition definition, QuestPlayerState state, out bool completed)
        {
            completed = false;
            var changed = false;
            var all = definition.Conditions.Count > 0;
            foreach (var condition in definition.Conditions)
            {
                var current = Current(condition, state);
                int old;
                if (!state.Progress.TryGetValue(condition.Id, out old) || old != current)
                {
                    state.Progress[condition.Id] = current;
                    changed = true;
                }
                if (current < Math.Max(1, condition.Target))
                    all = false;
            }
            if (all && state.State == QUEST_STATE_ACTIVE)
            {
                state.State = QUEST_STATE_COMPLETED;
                completed = changed = true;
            }
            return changed;
        }

        private int Current(QuestConditionDefinition condition, QuestPlayerState state)
        {
            if (string.Equals(condition.Type, "joinClan", StringComparison.OrdinalIgnoreCase))
            {
                int persisted;
                if (state.State == QUEST_STATE_COMPLETED && state.Progress.TryGetValue(condition.Id, out persisted))
                    return Math.Max(0, persisted);
                return Player != null && Player.Clan != null && Player.Clan.Id != 0 ? 1 : 0;
            }
            int value;
            return state.Progress.TryGetValue(condition.Id, out value) ? Math.Max(0, value) : 0;
        }

        private void PersistMutation(QuestPlayerState state)
        {
            persistenceDirty = true;
            dirtyQuestIds.Add(state.QuestId);
            if (QueryManager.SavePlayer.SaveQuestState(Player, state))
            {
                dirtyQuestIds.Remove(state.QuestId);
                persistenceDirty = dirtyQuestIds.Count > 0;
            }
        }

        private void RefreshCompatibilityState()
        {
            var state = states.Values.Where(x => x.State == QUEST_STATE_ACTIVE).OrderBy(x => x.QuestId).FirstOrDefault() ?? states.Values.Where(x => x.State == QUEST_STATE_COMPLETED).OrderBy(x => x.QuestId).FirstOrDefault();
            ActiveQuestId = state == null ? 0 : state.QuestId;
            QuestState = state == null ? QUEST_STATE_AVAILABLE : state.State;
        }

        public void SendModernQuestList()
        {
            SendModernQuestList(OpenQuestGiverId != 0);
        }

        private void SendModernQuestList(bool requireQuestGiver)
        {
            if (requireQuestGiver && !CanUseQuestGiver(OpenQuestGiverId))
                return;

            var quests = new List<QuestNettyModule>();
            foreach (var definition in QuestCatalog.All())
            {
                var state = GetState(definition.Id);
                if (!Include(definition, state))
                    continue;
                quests.Add(QuestNettyModule.FromWire(QuestListItemModule.write(definition.Id, definition.SortOrder, definition.MinLevel, definition.Priority, ModernStatus(definition, state), definition.Title, DescriptionKey(definition))));
            }
            // The wire order is daily slots first, normal slots second.
            Player.SendCommand(new QuestListUpdateCommand(0, GetNormalQuestSlotsRemaining(), false, quests).write());
        }

        private int GetNormalQuestSlotsRemaining()
        {
            var active = states.Values.Count(x => x.State == QUEST_STATE_ACTIVE);
            return Math.Max(0, MAX_NORMAL_QUEST_SLOTS - active);
        }

        private void SendModernQuestUpdate(QuestDefinition definition)
        {
            if (definition == null)
                return;
            var quest = BuildModernDefinition(definition);
            Player.SendCommand(new QuestUpdateCommand(quest).write());
        }

        private void SendConditionUpdates(QuestDefinition definition, QuestPlayerState state)
        {
            if (definition == null || state == null)
                return;

            foreach (var condition in definition.Conditions)
            {
                var completed = Current(condition, state) >= Math.Max(1, condition.Target);
                Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_UPDATE}|{definition.Id}|{condition.WireId}|{Current(condition, state)}|{(completed ? 1 : 0)}|{(state.State == QUEST_STATE_ACTIVE ? 1 : 0)}");
            }
        }

        private QuestNettyModule BuildModernDefinition(QuestDefinition definition)
        {
            var playerState = GetState(definition.Id);
            var icons = definition.Icon == 0
                ? new List<QuestNettyModule>()
                : new List<QuestNettyModule> { QuestNettyModule.FromWire(QuestIconModule.write(definition.Icon)) };
            var rewards = definition.Rewards
                .Where(x => !string.IsNullOrWhiteSpace(x.LootId) && x.Amount > 0)
                .Select(x => QuestNettyModule.FromWire(QuestRewardModule.write(x.LootId, x.Amount)))
                .ToList();
            var conditionTypes = definition.Conditions.Count == 0
                ? new List<QuestNettyModule>()
                : new List<QuestNettyModule> { QuestNettyModule.FromWire(QuestConditionTypeModule.write(0)) };
            var conditionRestrictions = new List<QuestNettyModule>();
            foreach (var condition in definition.Conditions)
            {
                var current = Current(condition, playerState);
                var completed = current >= Math.Max(1, condition.Target);
                var conditionState = QuestNettyModule.FromWire(QuestConditionStateModule.write(
                    current, playerState.State == QUEST_STATE_ACTIVE, completed));
                var entries = string.IsNullOrWhiteSpace(condition.DescriptionKey)
                    ? new List<string>()
                    : new List<string> { condition.DescriptionKey };
                var conditionModule = QuestNettyModule.FromWire(QuestConditionModule.write(
                    condition.WireId, entries, condition.WireType, 0, condition.Target, false,
                    conditionState, new List<QuestNettyModule>()));
                var conditionRestriction = QuestNettyModule.FromWire(QuestConditionWrapperModule.write(
                    conditionModule, QuestNettyModule.FromWire(QuestRestrictionModule.write())));
                conditionRestrictions.Add(conditionRestriction);
            }
            var restriction = QuestNettyModule.FromWire(QuestRestrictionModule.write(
                0, IsEligible(definition), false, definition.MinLevel, true, conditionRestrictions));
            return QuestNettyModule.FromWire(QuestDefinitionModule.write(
                definition.Id, definition.Title, DescriptionKey(definition), restriction, icons, rewards, conditionTypes));
        }

        private void SendQuestCompleted(QuestDefinition definition)
        {
            GrantRewards(definition);
            Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_ACCOMPLISH_QUEST}|{definition.Id}");
            var replacements = new List<MessageWildcardReplacementModule> { new MessageWildcardReplacementModule("%quest_name%", definition.Title, new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.PLAIN)) };
            Player.SendCommand(new MessageLocalizedWildcardCommand("q2_accomplished_quest", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), replacements).write());
            Player.SendPacket($"0|A|STD|[Quest] Completed quest {definition.Id}: {definition.Title}");
        }

        private bool Include(QuestDefinition definition, QuestPlayerState state)
        {
            var f = Player.Settings == null ? null : Player.Settings.ClassY2T;
            if (f == null || (!f.questsAvailableFilter && !f.questsUnavailableFilter && !f.questsCompletedFilter))
                return true;
            if (state.State == QUEST_STATE_AVAILABLE && IsEligible(definition)) return f.questsAvailableFilter;
            if (state.State == QUEST_STATE_COMPLETED) return f.questsCompletedFilter;
            return f.questsUnavailableFilter;
        }

        private bool IsEligible(QuestDefinition definition)
        {
            return definition != null && Player != null && Player.Level >= definition.MinLevel &&
                   (definition.FactionId == 0 || definition.FactionId == Player.FactionId);
        }

        private short ModernStatus(QuestDefinition definition, QuestPlayerState state)
        {
            if (state.State == QUEST_STATE_COMPLETED) return 3;
            if (state.State == QUEST_STATE_ACTIVE) return 2;
            return (short)(IsEligible(definition) ? 1 : 0);
        }

        private void GrantRewards(QuestDefinition definition)
        {
            foreach (var reward in definition.Rewards.Where(x => x.Amount > 0 && !string.IsNullOrWhiteSpace(x.LootId)))
            {
                switch (reward.LootId.Trim().ToLowerInvariant())
                {
                    case "credits":
                    case "currency_credits":
                        Player.ChangeData(DataType.CREDITS, reward.Amount);
                        break;
                    case "uridium":
                    case "currency_uridium":
                        Player.ChangeData(DataType.URIDIUM, reward.Amount);
                        break;
                    case "honor":
                    case "currency_honor":
                        Player.ChangeData(DataType.HONOR, reward.Amount);
                        break;
                    case "experience":
                    case "ep":
                        Player.ChangeData(DataType.EXPERIENCE, reward.Amount);
                        break;
                    case "jackpot":
                        Player.ChangeData(DataType.JACKPOT, reward.Amount);
                        break;
                    default:
                        Logger.Log("error_log", $"- [Quests.cs] Unsupported reward '{reward.LootId}' in quest {definition.Id}.");
                        break;
                }
            }
        }

        private string DescriptionKey(QuestDefinition q)
        {
            var key = string.IsNullOrWhiteSpace(q.DescriptionKey) ? q.Description : q.DescriptionKey;
            if (string.IsNullOrWhiteSpace(key)) key = "quest_description_" + q.Id;
            return key.Replace("{faction}", Player != null && Player.FactionId == 2 ? "eic" : Player != null && Player.FactionId == 3 ? "vru" : "mmo");
        }

        private static bool ReadInt(DataRow row, string column, out int value)
        {
            value = 0;
            return row != null && row.Table.Columns.Contains(column) && row[column] != DBNull.Value && int.TryParse(Convert.ToString(row[column]), out value);
        }
    }
}
