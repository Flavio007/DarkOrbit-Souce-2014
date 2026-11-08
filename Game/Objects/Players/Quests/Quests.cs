using Ow.Game.Objects.Players.Managers;
using Ow.Game.Objects.Stations;
using Ow.Managers;
using Ow.Managers.MySQLManager;
using Newtonsoft.Json.Linq;
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
using System.Threading.Tasks;

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
        public int TargetShipId { get; set; }
        public int TargetOre { get; set; }
        public string IconKey { get; set; }
        public string TargetNpcName { get; set; }
        public string Branch { get; set; }
        public int RequireCloak { get; set; }
        public int MaxHpPercent { get; set; } = 100;
        public bool InRadiation { get; set; }
        public int MapSectorMax { get; set; }
        public string TargetShipIds { get; set; }
        public string ShipFamily { get; set; }
        public string TargetMaps { get; set; }
        public int MapOwner { get; set; }
        public int MapSector { get; set; }
        public bool EnemyTarget { get; set; }
        public int TargetMapId { get; set; }
        public int TargetFactionId { get; set; }
        public int Sequence { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Radius { get; set; }
        public string Weapon { get; set; }
        public string AlternativeGroup { get; set; }
        public int MaxAbsenceSeconds { get; set; }
        public bool Continuous { get; set; }
        public string MapRelation { get; set; }
        private bool MatchesNpcName(int shipId)
        {
            var ship = GameManager.GetShip(shipId);
            if (ship == null) return false;
            Func<string, string> normalize = value => new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return normalize(ship.Name) == normalize(TargetNpcName);
        }

        private bool MatchesShipFamily(int shipId)
        {
            var ship = GameManager.GetShip(shipId);
            if (ship == null) return false;
            return ShipFamily.Split(',').Any(family =>
            {
                var key = "ship_" + family.Trim().ToLowerInvariant();
                return string.Equals(ship.LootId, key, StringComparison.OrdinalIgnoreCase) ||
                    (ship.LootId ?? "").StartsWith(key + "_design_", StringComparison.OrdinalIgnoreCase);
            });
        }

        public bool MatchesEvent(string type, int shipId, int ore)
        {
            return string.Equals(Type, type, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(TargetNpcName) || MatchesNpcName(shipId)) &&
                (TargetShipId == 0 || TargetShipId == shipId) &&
                (string.IsNullOrWhiteSpace(TargetShipIds) || TargetShipIds.Split(',').Any(id => id.Trim() == shipId.ToString())) &&
                (string.IsNullOrWhiteSpace(ShipFamily) || MatchesShipFamily(shipId)) && (TargetOre == 0 || TargetOre == ore);
        }
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
        public bool Daily { get; set; }
        public string ForbiddenKillShipIds { get; set; }
        public string RequiredAmmo { get; set; }
        public int MaxTravel { get; set; } = -1;
        public bool ForbidCloak { get; set; }
        public string ForbiddenWeapon { get; set; }
        public int TimeLimitSeconds { get; set; }
        public int MaxKills { get; set; } = -1;
        public int MaxDeaths { get; set; } = -1;
        public int MaxDamageTaken { get; set; } = -1;
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
        private static readonly string ConfigFile = Path.Combine("config", "quests.xml");
        private static readonly object SyncRoot = new object();
        private static Dictionary<int, QuestDefinition> definitions;

        public static List<QuestDefinition> All()
        {
            EnsureLoaded();
            lock (SyncRoot)
                return definitions.Values.OrderBy(x => x.MinLevel).ThenBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
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
                                Daily = Int(e, "daily", 0) != 0,
                                ForbiddenKillShipIds = Value(e, "forbiddenKillShipIds", ""),
                                RequiredAmmo = Value(e, "requiredAmmo", ""),
                                MaxTravel = Int(e, "maxTravel", -1),
                                ForbidCloak = Int(e, "forbidCloak", 0) != 0,
                                ForbiddenWeapon = Value(e, "forbiddenWeapon", ""),
                                TimeLimitSeconds = Math.Max(0, Int(e, "timeLimitSeconds", 0)),
                                MaxKills = Int(e, "maxKills", -1),
                                MaxDeaths = Int(e, "maxDeaths", -1),
                                MaxDamageTaken = Int(e, "maxDamageTaken", -1),
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
                                        TargetNpcName = Value(c, "targetNpcName", ""),
                                        Branch = Value(c, "branch", ""),
                                        RequireCloak = Int(c, "requireCloak", 0),
                                        MaxHpPercent = Int(c, "maxHpPercent", 100),
                                        InRadiation = Int(c, "inRadiation", 0) != 0,
                                        MapSectorMax = Int(c, "mapSectorMax", 0),
                                        TargetShipIds = Value(c, "targetShipIds", ""),
                                        ShipFamily = Value(c, "shipFamily", ""),
                                        TargetMaps = Value(c, "targetMaps", ""),
                                        MapOwner = Int(c, "mapOwner", 0),
                                        MapSector = Int(c, "mapSector", 0),
                                        EnemyTarget = Int(c, "enemyTarget", 0) != 0,
                                        TargetShipId = Int(c, "targetShipId", 0),
                                        TargetOre = Int(c, "targetOre", 0),
                                        IconKey = Value(c, "iconKey", ""),
                                        TargetMapId = Int(c, "targetMapId", 0),
                                        TargetFactionId = Int(c, "targetFactionId", 0),
                                        Sequence = Int(c, "sequence", 0),
                                        MaxAbsenceSeconds = Math.Max(0, Int(c, "maxAbsenceSeconds", 0)),
                                        Continuous = Int(c, "continuous", 0) != 0,
                                        MapRelation = Value(c, "mapRelation", ""),
                                        Weapon = Value(c, "weapon", ""),
                                        AlternativeGroup = Value(c, "alternativeGroup", ""),
                                        X = Int(c, "x", 0),
                                        Y = Int(c, "y", 0),
                                        Radius = Math.Max(1, Int(c, "radius", 300)),
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
                TitleKey = "quest_title_first_assignment",
                Description = "Voe ate a estacao de controle de missoes da sua companhia no mapa inicial.",
                DescriptionKey = "quest_description_first_assignment",
                SortOrder = 1,
                MinLevel = 1
            };
            q.Conditions.Add(new QuestConditionDefinition { Id = "visit_mission_control", Type = "visitMissionControl", DescriptionKey = "starter_objective_mission_control", Target = 1, WireId = 1, WireType = 0 });
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
                case "collectore": return 5;
                case "collectbonusbox": return 52;
                case "visitmap": return 31;
                case "jump": return 39;
                case "travel": return 13;
                case "stayonmap": return 31;
                case "visitposition": return 0;
                case "damage": return 0;
                case "sellore": return 5;
                case "collectcargoore": return 5;
                case "collectcargobox": return 5;
                case "radiation": return 0;
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

        public const string QuestName = "Primeiros passos";
        public Player Player { get; set; }
        public int QuestState = QUEST_STATE_AVAILABLE;
        public int ActiveQuestId = 0;
        public int OpenQuestGiverId { get; private set; }
        public bool CanDie = true;
        public string LoadLootId { get; set; }
        public bool ReloadingActive = false;

        private const int MAX_NORMAL_QUEST_SLOTS = 5;
        private const int MAX_DAILY_QUEST_SLOTS = 12;
        private readonly Dictionary<int, QuestPlayerState> states = new Dictionary<int, QuestPlayerState>();
        private readonly HashSet<int> dirtyQuestIds = new HashSet<int>();
        private bool persistenceDirty;

        public Quests(Player player)
        {
            Player = player;
            LoadLootId = AmmunitionManager.ROCKET_LAUNCHER_ECO_10;
            InitializeCatalogStates();
        }

        private DateTime lastObjectiveTick = DateTime.UtcNow;

        public void Tick()
        {
            ResetDailyQuests();
            if (OpenQuestGiverId != 0 && !CanUseQuestGiver(OpenQuestGiverId))
                OpenQuestGiverId = 0;
            var now = DateTime.UtcNow;
            if ((now - lastObjectiveTick).TotalSeconds >= 1)
            {
                // Count observed online seconds only; reconnects never award offline time.
                lastObjectiveTick = now;
                EnforceRestrictions();
                ResetContinuousObjectives();
                if (Player != null && !Player.Destroyed && Player.Spacemap != null && Player.Position != null)
                {
                    ReportProgress("stayOnMap");
                    ReportProgress("visitMap");
                    ReportProgress("visitPosition");
                    if (Player.Spacemap.Activatables.Values.OfType<Portal>().Any(portal => Player.Position.DistanceTo(portal.Position) <= 400)) ReportProgress("visitPortal");
                    if (Player.Spacemap.Activatables.Values.OfType<HomeStation>().Any(station => station.FactionId == Player.FactionId && Player.Position.DistanceTo(station.Position) <= 700)) ReportProgress("visitHomeBase");
                    ReportProgress("elapsed");
                }
                if (Player != null && !Player.Destroyed && Player.Storage.IsInRadiationZone) ReportProgress("radiation");
            }
            TryCompleteClanQuest();
        }

        public void OpenWindow()
        {
            ResetDailyQuests();
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
            EnsureStarterMission();
            SyncAcceptedTracker();
        }

        private void EnsureStarterMission()
        {
            if (Player?.Spacemap == null || Player.Spacemap.VisualMapId == Ow.Game.Events.TutorialManager.TutorialMapId)
                return;
            var state = GetStateIfKnown(QUEST_ID_PARTING_ADVICE);
            if (state == null || state.State != QUEST_STATE_AVAILABLE) return;
            state.State = QUEST_STATE_ACTIVE;
            state.Progress.Clear();
            PersistMutation(state);
            RefreshCompatibilityState();
            SendModernQuestUpdate(QuestCatalog.Get(QUEST_ID_PARTING_ADVICE));
            var station = Player.Spacemap.Activatables.Values.OfType<QuestGiverStation>().FirstOrDefault(x => x.FactionId == Player.FactionId);
            if (station != null) _ = PreviewMissionControlAsync(station);
        }

        private async Task PreviewMissionControlAsync(QuestGiverStation station)
        {
            try
            {
                await Task.Delay(600);
                if (Player?.Spacemap != station.Spacemap || GameManager.GetGameSession(Player.Id)?.Player != Player ||
                    GetStateIfKnown(QUEST_ID_PARTING_ADVICE)?.State != QUEST_STATE_ACTIVE) return;
                Player.SendPacket("0|A|STD|Voe ate esta estacao de controle de missoes para receber suas primeiras ordens.");
                await Player.CameraVisitAsync(station.Position.X, station.Position.Y, 1.5, 1.25);
            }
            catch (Exception e) { Logger.Log("error_log", $"- [Quests.cs] PreviewMissionControlAsync: {e}"); }
        }

        // Snapshot eligibility before destruction clears targets and combat state.
        public static List<Player> KillRecipients(Player killer, Attackable victim)
        {
            var recipients = new List<Player>();
            if (killer == null) return recipients;
            recipients.Add(killer);
            var group = killer.Group;
            if (group == null || victim?.Spacemap == null || !(victim is Npc || victim is Player)) return recipients;
            var now = DateTime.Now;
            foreach (var member in group.Members.Values.ToArray())
            {
                if (member == null || member == killer || ReferenceEquals(member, victim) ||
                    member.Group != group || member.Destroyed || member.GameSession == null ||
                    member.Spacemap != victim.Spacemap) continue;
                Attacker attack;
                var attackingVictim = victim.Attackers.TryGetValue(member.Id, out attack) &&
                    ReferenceEquals(attack.Player, member) && attack.LastRefresh.AddSeconds(10) > now;
                // A lethal first shot is recorded before lastAttackTime is updated.
                if (!member.AttackingOrUnderAttack() && !attackingVictim) continue;
                if (victim is Player)
                {
                    if (!ReferenceEquals(member.Selected, victim) || !attackingVictim ||
                        !(member.AttackManager.Attacking || attack.QuestProjectileTime.AddSeconds(3) > now)) continue;
                }
                if (!recipients.Contains(member)) recipients.Add(member);
            }
            return recipients;
        }

        private int highlightedQuestId;
        public void HighlightAcceptedQuest(int questId)
        {
            var state = GetStateIfKnown(questId);
            if (state == null || state.State != QUEST_STATE_ACTIVE || !BelongsToFaction(QuestCatalog.Get(questId))) return;
            highlightedQuestId = questId;
            Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_HIGHLIGHT_QUEST}|{questId}");
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

            var highlighted = accepted.FirstOrDefault(state => state.QuestId == highlightedQuestId) ?? accepted.FirstOrDefault();
            if (highlighted != null)
                highlightedQuestId = highlighted.QuestId;
            if (highlighted != null)
                Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_HIGHLIGHT_QUEST}|{highlighted.QuestId}");
        }

        public bool HandleAcceptQuest(int questId)
        {
            ResetDailyQuests();
            var definition = QuestCatalog.Get(questId);
            if (definition == null || !CanUseQuestGiver(OpenQuestGiverId) || !IsEligible(definition))
                return false;
            var state = GetState(questId);
            if (state.State == QUEST_STATE_COMPLETED || state.State == QUEST_STATE_ACTIVE)
                return true;

            if (AcceptedStates().Count(active => QuestCatalog.Get(active.QuestId).Daily == definition.Daily) >= (definition.Daily ? MAX_DAILY_QUEST_SLOTS : MAX_NORMAL_QUEST_SLOTS)) return false;
            state.Progress.Clear();
            state.State = QUEST_STATE_ACTIVE;
            state.Progress["$started"] = (int)(DateTime.UtcNow - new DateTime(2020, 1, 1)).TotalSeconds;
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
            if (definition == null || (definition.FactionId != 0 && definition.FactionId != Player.FactionId))
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
            EnforceRestrictions();
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

        public int ReportProgress(string conditionType, int amount = 1, int targetShipId = 0, int targetOre = 0, int targetFactionId = 0, string weapon = "")
        {
            if (string.IsNullOrWhiteSpace(conditionType) || amount <= 0)
                return 0;

            if (string.Equals(conditionType, "travel", StringComparison.OrdinalIgnoreCase))
                foreach (var active in AcceptedStates())
                {
                    int traveled;
                    active.Progress.TryGetValue("$travel", out traveled);
                    active.Progress["$travel"] = (int)Math.Min(int.MaxValue, (long)traveled + amount);
                    PersistMutation(active);
                }
            EnforceRestrictions();
            var updates = new List<Tuple<int, string, int>>();
            foreach (var state in states.Values.Where(x => x.State == QUEST_STATE_ACTIVE))
            {
                var definition = QuestCatalog.Get(state.QuestId);
                if (definition == null || !BelongsToFaction(definition))
                    continue;
                foreach (var condition in definition.Conditions.Where(x => x.MatchesEvent(conditionType, targetShipId, targetOre) &&
                    MatchesMap(x) &&
                    (!x.EnemyTarget || targetFactionId >= 1 && targetFactionId <= 3 && targetFactionId != Player.FactionId) &&
                    (x.TargetFactionId == 0 || x.TargetFactionId == targetFactionId) &&
                    (string.IsNullOrEmpty(x.Weapon) || string.Equals(x.Weapon, weapon, StringComparison.OrdinalIgnoreCase)) &&
                    (x.Sequence == 0 || definition.Conditions.Where(previous => previous.Branch == x.Branch && previous.Sequence > 0 && previous.Sequence < x.Sequence).All(previous => Current(previous, state) >= previous.Target)) &&
                    (!string.Equals(conditionType, "visitPosition", StringComparison.OrdinalIgnoreCase) || Player.Position != null &&
                        Math.Pow((double)Player.Position.X - x.X, 2) + Math.Pow((double)Player.Position.Y - x.Y, 2) <= (double)x.Radius * x.Radius)))
                {
                    var current = Current(condition, state);
                    updates.Add(Tuple.Create(definition.Id, condition.Id, (int)Math.Min(int.MaxValue, (long)current + amount)));
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
            var starter = GetStateIfKnown(QUEST_ID_PARTING_ADVICE);
            if (starter != null && (starter.Progress.ContainsKey("join_clan") ||
                (starter.State == QUEST_STATE_COMPLETED && !starter.Progress.ContainsKey("visit_mission_control"))))
            {
                starter.State = QUEST_STATE_AVAILABLE;
                starter.Progress.Clear();
            }
            // A continuous stay cannot span a disconnect.
            foreach (var active in AcceptedStates())
                foreach (var condition in QuestCatalog.Get(active.QuestId).Conditions.Where(c => c.Continuous))
                    if (Current(condition, active) < condition.Target) active.Progress[condition.Id] = 0;
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
            return states.Values.Where(x => x.State == QUEST_STATE_ACTIVE && BelongsToFaction(QuestCatalog.Get(x.QuestId))).OrderBy(x => QuestCatalog.Get(x.QuestId).MinLevel).ThenBy(x => QuestCatalog.Get(x.QuestId).SortOrder).ThenBy(x => x.QuestId).ToList();
        }

        private static int DailyDate => (int)(DateTime.UtcNow.Date - new DateTime(2020, 1, 1)).TotalDays;

        private void ResetDailyQuests()
        {
            foreach (var state in states.Values.Where(state => state.State == QUEST_STATE_COMPLETED).ToList())
            {
                var definition = QuestCatalog.Get(state.QuestId);
                int completedDay;
                if (definition == null || !definition.Daily || !state.Progress.TryGetValue("$completedDay", out completedDay) || completedDay >= DailyDate) continue;
                state.State = QUEST_STATE_AVAILABLE;
                state.Progress.Clear();
                PersistMutation(state);
                RefreshCompatibilityState();
            }
        }

        private bool MatchesMap(QuestConditionDefinition condition)
        {
            if (Player?.Spacemap == null) return false;
            if (condition.RequireCloak == 1 && !Player.Invisible || condition.RequireCloak == -1 && Player.Invisible) return false;
            if (condition.MaxHpPercent < 100 && (long)Player.CurrentHitPoints * 100 >= (long)Player.MaxHitPoints * condition.MaxHpPercent) return false;
            if (condition.InRadiation && !Player.Storage.IsInRadiationZone) return false;
            if (condition.MapSectorMax > 0 && (LowMapRadar.Sector(Player.Spacemap.VisualMapId) == 0 || LowMapRadar.Sector(Player.Spacemap.VisualMapId) > condition.MapSectorMax)) return false;
            if (!string.IsNullOrWhiteSpace(condition.TargetMaps) && !condition.TargetMaps.Split(',').Any(name => string.Equals(name.Trim(), Player.Spacemap.Name, StringComparison.OrdinalIgnoreCase))) return false;
            if (condition.MapOwner > 0 && Player.Spacemap.FactionId != condition.MapOwner) return false;
            if (condition.MapSector > 0)
            {
                var pieces = (Player.Spacemap.Name ?? "").Split('-');
                int sector;
                if (pieces.Length != 2 || pieces[0] == "4" || !int.TryParse(pieces[1], out sector) || sector != condition.MapSector) return false;
            }
            if (condition.TargetMapId != 0 && Player.Spacemap.VisualMapId != condition.TargetMapId) return false;
            if (string.Equals(condition.MapRelation, "enemy", StringComparison.OrdinalIgnoreCase))
                return Player.Spacemap.FactionId > 0 && Player.Spacemap.FactionId != Player.FactionId;
            if (string.Equals(condition.MapRelation, "home", StringComparison.OrdinalIgnoreCase))
                return Player.Spacemap.FactionId == Player.FactionId;
            return true;
        }

        private void ResetContinuousObjectives()
        {
            var now = (int)(DateTime.UtcNow - new DateTime(2020, 1, 1)).TotalSeconds;
            foreach (var state in AcceptedStates())
            {
                var definition = QuestCatalog.Get(state.QuestId);
                foreach (var condition in definition.Conditions.Where(c => c.Continuous || c.MaxAbsenceSeconds > 0))
                {
                    if (Current(condition, state) >= condition.Target) continue;
                    var key = "$away:" + condition.Id;
                    if (Current(condition, state) == 0 && !state.Progress.ContainsKey(key)) continue;
                    if (MatchesMap(condition) && !Player.Destroyed)
                    {
                        if (state.Progress.Remove(key)) PersistMutation(state);
                        continue;
                    }
                    int away;
                    if (!state.Progress.TryGetValue(key, out away))
                    {
                        state.Progress[key] = now;
                        PersistMutation(state);
                        away = now;
                    }
                    if (condition.MaxAbsenceSeconds > 0 && now - away > condition.MaxAbsenceSeconds)
                    {
                        HandleAbortQuest(definition.Id);
                        break;
                    }
                    if (condition.Continuous && Current(condition, state) > 0)
                    {
                        state.Progress[condition.Id] = 0;
                        PersistMutation(state);
                        SendConditionUpdates(definition, state);
                    }
                }
            }
        }

        public void CheckForbiddenKill(int shipId)
        {
            foreach (var active in AcceptedStates())
            {
                var filter = QuestCatalog.Get(active.QuestId).ForbiddenKillShipIds;
                if (!string.IsNullOrEmpty(filter) && filter.Split(',').Any(id => id.Trim() == shipId.ToString()))
                    HandleAbortQuest(active.QuestId);
            }
        }

        public void RecordAmmo(string ammo)
        {
            foreach (var active in AcceptedStates())
            {
                var required = QuestCatalog.Get(active.QuestId).RequiredAmmo;
                if (!string.IsNullOrEmpty(required) && !string.Equals(required, ammo, StringComparison.OrdinalIgnoreCase))
                    HandleAbortQuest(active.QuestId);
            }
        }

        public void RecordWeapon(string weapon)
        {
            foreach (var state in AcceptedStates())
            {
                var definition = QuestCatalog.Get(state.QuestId);
                if (!string.IsNullOrEmpty(definition.ForbiddenWeapon) &&
                    (definition.ForbiddenWeapon == "ANY" || string.Equals(definition.ForbiddenWeapon, weapon, StringComparison.OrdinalIgnoreCase)))
                    HandleAbortQuest(definition.Id);
            }
        }

        public void RecordDamage(int amount)
        {
            if (amount <= 0) return;
            foreach (var state in AcceptedStates())
            {
                int previous;
                state.Progress.TryGetValue("$damage", out previous);
                state.Progress["$damage"] = (int)Math.Min(int.MaxValue, (long)previous + amount);
                PersistMutation(state);
            }
            EnforceRestrictions();
            ReportProgress("damageTaken", amount);
        }

        public void RecordKill()
        {
            foreach (var state in AcceptedStates())
            {
                int previous;
                state.Progress.TryGetValue("$kills", out previous);
                state.Progress["$kills"] = previous + 1;
                PersistMutation(state);
            }
            EnforceRestrictions();
        }

        public void OnCloakChanged() { EnforceRestrictions(); }

        public void RecordDeath()
        {
            foreach (var state in AcceptedStates())
            {
                int previous;
                state.Progress.TryGetValue("$deaths", out previous);
                state.Progress["$deaths"] = previous + 1;
                PersistMutation(state);
            }
            EnforceRestrictions();
        }

        private void EnforceRestrictions()
        {
            var now = (int)(DateTime.UtcNow - new DateTime(2020, 1, 1)).TotalSeconds;
            foreach (var state in AcceptedStates())
            {
                var definition = QuestCatalog.Get(state.QuestId);
                int started, deaths, damage, kills, traveled;
                state.Progress.TryGetValue("$travel", out traveled);
                state.Progress.TryGetValue("$kills", out kills);
                state.Progress.TryGetValue("$started", out started);
                state.Progress.TryGetValue("$deaths", out deaths);
                state.Progress.TryGetValue("$damage", out damage);
                if ((definition.MaxTravel >= 0 && traveled > definition.MaxTravel) ||
                    (definition.ForbidCloak && Player.Invisible) ||
                    (definition.TimeLimitSeconds > 0 && started > 0 && now - started >= definition.TimeLimitSeconds) ||
                    (definition.MaxKills >= 0 && kills > definition.MaxKills) ||
                    (definition.MaxDeaths >= 0 && deaths > definition.MaxDeaths) ||
                    (definition.MaxDamageTaken >= 0 && damage > definition.MaxDamageTaken))
                {
                    HandleAbortQuest(definition.Id);
                    Player.SendPacket("0|A|STD|Missão cancelada: prazo ou limite excedido: " + definition.Title);
                }
            }
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
            if (definition.Conditions.Count > 0)
            {
                Func<IEnumerable<QuestConditionDefinition>, bool> complete = conditions => conditions
                    .GroupBy(c => string.IsNullOrEmpty(c.AlternativeGroup) ? "condition:" + c.Id : "alternative:" + c.AlternativeGroup)
                    .All(group => group.Any(c => Current(c, state) >= c.Target));
                var mandatory = definition.Conditions.Where(c => string.IsNullOrEmpty(c.Branch));
                var branches = definition.Conditions.Where(c => !string.IsNullOrEmpty(c.Branch)).GroupBy(c => c.Branch).ToList();
                all = complete(mandatory) && (branches.Count == 0 || branches.Any(branch => complete(branch)));
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
            if (state.State == QUEST_STATE_COMPLETED) return Math.Max(1, condition.Target);
            if (string.Equals(condition.Type, "visitMissionControl", StringComparison.OrdinalIgnoreCase))
            {
                int visited;
                if (state.Progress.TryGetValue(condition.Id, out visited) && visited > 0) return 1;
                if (Player?.Spacemap == null || Player.Spacemap.VisualMapId != Player.GetBaseMapId()) return 0;
                return Player.Spacemap.Activatables.Values.OfType<QuestGiverStation>()
                    .Any(station => CanUseQuestGiver(station.Id)) ? 1 : 0;
            }
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
                quests.Add(QuestNettyModule.FromWire(QuestListItemModule.write(definition.Id, definition.MinLevel * 1000 + definition.SortOrder, definition.MinLevel, definition.Priority, ModernStatus(definition, state), definition.Title, QuestTitleKey(definition))));
            }
            // The wire order is daily slots first, normal slots second.
            Player.SendCommand(new QuestListUpdateCommand(0, GetQuestSlotsRemaining(), false, quests).write());
        }

        private int GetQuestSlotsRemaining()
        {
            var active = AcceptedStates();
            var daily = active.Count(x => QuestCatalog.Get(x.QuestId).Daily);
            return Math.Max(0, MAX_NORMAL_QUEST_SLOTS - (active.Count - daily)) + Math.Max(0, MAX_DAILY_QUEST_SLOTS - daily);
        }

        private void SendModernQuestUpdate(QuestDefinition definition)
        {
            if (definition == null)
                return;
            var quest = BuildModernDefinition(definition);
            Player.SendCommand(new QuestUpdateCommand(quest).write());
        }

        private bool ConditionActive(QuestDefinition definition, QuestPlayerState state, QuestConditionDefinition condition)
        {
            return state.State == QUEST_STATE_ACTIVE &&
                (condition.Sequence == 0 || definition.Conditions
                    .Where(previous => previous.Branch == condition.Branch && previous.Sequence > 0 && previous.Sequence < condition.Sequence)
                    .All(previous => Current(previous, state) >= previous.Target));
        }

        private void SendConditionUpdates(QuestDefinition definition, QuestPlayerState state)
        {
            if (definition == null || state == null)
                return;

            foreach (var condition in definition.Conditions)
            {
                var completed = Current(condition, state) >= Math.Max(1, condition.Target);
                Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_UPDATE}|{definition.Id}|{condition.WireId}|{Current(condition, state)}|{(completed ? 1 : 0)}|{(ConditionActive(definition, state, condition) ? 1 : 0)}");
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
                    current, ConditionActive(definition, playerState, condition), completed));
                var entries = string.IsNullOrWhiteSpace(condition.DescriptionKey)
                    ? new List<string>()
                    : new List<string> { condition.DescriptionKey };
                if (!string.IsNullOrWhiteSpace(condition.IconKey)) entries.Add("icon:" + condition.IconKey);
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
                definition.Id, definition.Title, QuestTitleKey(definition), restriction, icons, rewards, conditionTypes));
        }

        private void SendQuestCompleted(QuestDefinition definition)
        {
            if (definition.Daily)
            {
                var state = GetState(definition.Id);
                state.Progress["$completedDay"] = DailyDate;
                PersistMutation(state);
            }
            GrantRewards(definition);
            QueryManager.SavePlayer.Information(Player);
            if (definition.Rewards.Any(reward => reward.LootId.StartsWith("ammunition_", StringComparison.OrdinalIgnoreCase)))
                QueryManager.SavePlayer.Ammo(Player);
            QueryManager.WriteLogbook(Player,"quest","Missão concluída: "+definition.Title+".");
            Player.Achievements?.Report("quest");
            Player.SendPacket($"0|{ServerCommands.QUESTFM_INFO}|{ServerCommands.QUESTFM_ACCOMPLISH_QUEST}|{definition.Id}");
            var replacements = new List<MessageWildcardReplacementModule> { new MessageWildcardReplacementModule("%quest_name%", definition.Title, new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.PLAIN)) };
            Player.SendCommand(new MessageLocalizedWildcardCommand("q2_accomplished_quest", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), replacements).writeCommand());
            SendModernQuestList(false);
        }

        private bool Include(QuestDefinition definition, QuestPlayerState state)
        {
            if (!BelongsToFaction(definition)) return false;
            if (state.State == QUEST_STATE_ACTIVE)
                return true;
            var f = Player.Settings == null ? null : Player.Settings.ClassY2T;
            if (f == null || (!f.questsAvailableFilter && !f.questsUnavailableFilter && !f.questsCompletedFilter))
                return true;
            if (state.State == QUEST_STATE_AVAILABLE && IsEligible(definition)) return f.questsAvailableFilter;
            if (state.State == QUEST_STATE_COMPLETED) return f.questsCompletedFilter;
            return f.questsUnavailableFilter;
        }

        private bool BelongsToFaction(QuestDefinition definition)
        {
            return definition != null && Player != null && (definition.FactionId == 0 || definition.FactionId == Player.FactionId);
        }

        private bool IsEligible(QuestDefinition definition)
        {
            if (definition == null || Player == null || Player.Level < definition.MinLevel || !BelongsToFaction(definition)) return false;
            var state = GetStateIfKnown(definition.Id);
            if (state != null && state.State == QUEST_STATE_ACTIVE) return true;
            return AcceptedStates().Count(active => QuestCatalog.Get(active.QuestId).Daily == definition.Daily) <
                (definition.Daily ? MAX_DAILY_QUEST_SLOTS : MAX_NORMAL_QUEST_SLOTS);
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
                    case "extraenergy": Player.Data.extraEnergy += reward.Amount; break;
                    case "repaircredits": Player.Data.repairCredits += reward.Amount; break;
                    case "jumpcredits": Player.Data.jumpCredits += reward.Amount; break;
                    case "bootykeys":
                        Player.Data.bootyKeys = checked(Player.Data.bootyKeys + reward.Amount);
                        Player.Equipment.Items.BootyKeys = Player.Data.bootyKeys;
                        Player.SendPacket("0|A|BK|" + Player.Data.bootyKeys);
                        break;
                    case "logdisks":
                        GrantLogdisks(reward.Amount);
                        break;
                    default:
                        if (reward.LootId.StartsWith("ammunition_", StringComparison.OrdinalIgnoreCase))
                        {
                            Player.AddAmmo(reward.LootId, reward.Amount);
                            break;
                        }
                        Logger.Log("error_log", $"- [Quests.cs] Unsupported reward '{reward.LootId}' in quest {definition.Id}.");
                        break;
                }
            }
        }

        private void GrantLogdisks(int amount)
        {
            lock (Ow.Game.Events.AuctionManager.Sync)
                using (var client = SqlDatabaseManager.GetClient())
                {
                    var row = client.ExecuteQueryRow($"SELECT items FROM player_equipment WHERE userId = {Player.Id}");
                    var items = JObject.Parse(Convert.ToString(row["items"]));
                    var skills = items["skillTree"] as JObject ?? new JObject();
                    skills["logdisks"] = checked(((int?)skills["logdisks"] ?? 0) + amount);
                    items["skillTree"] = skills;
                    client.SetParameter("@questItems", items.ToString(Newtonsoft.Json.Formatting.None));
                    client.ExecuteNonQuery($"UPDATE player_equipment SET items = @questItems WHERE userId = {Player.Id}");
                }
        }

        private string DescriptionKey(QuestDefinition q)
        {
            var key = string.IsNullOrWhiteSpace(q.DescriptionKey) ? q.Description : q.DescriptionKey;
            if (string.IsNullOrWhiteSpace(key)) key = "quest_description_" + q.Id;
            return key.Replace("{faction}", Player != null && Player.FactionId == 2 ? "eic" : Player != null && Player.FactionId == 3 ? "vru" : "mmo");
        }

        private static string QuestTitleKey(QuestDefinition q)
        {
            if (q == null)
                return "0";

            var key = string.IsNullOrWhiteSpace(q.TitleKey) ? "" : q.TitleKey.Trim();
            const string prefix = "quest_title_";
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                key = key.Substring(prefix.Length);

            return string.IsNullOrWhiteSpace(key) ? q.Id.ToString() : key;
        }

        private static bool ReadInt(DataRow row, string column, out int value)
        {
            value = 0;
            return row != null && row.Table.Columns.Contains(column) && row[column] != DBNull.Value && int.TryParse(Convert.ToString(row[column]), out value);
        }
    }
}
