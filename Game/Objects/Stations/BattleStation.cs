using Ow.Game;
using Ow.Game.Movements;
using Ow.Game.Objects;
using Ow.Managers;
using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Game.Objects.Stations
{
    public class EquippedModuleBase
    {
        public int ClanId { get; set; }
        public List<SatelliteBase> Modules { get; set; }

        public EquippedModuleBase(int clanId, List<SatelliteBase> satellites)
        {
            ClanId = clanId;
            Modules = satellites;
        }
    }

    class BattleStation : Activatable
    {
        public Dictionary<int, List<Satellite>> EquippedStationModule = new Dictionary<int, List<Satellite>>();

        public bool InBuildingState = false;
        public int BuildTimeInMinutes = 0;

        public bool DeflectorActive = false;
        public int DeflectorSecondsLeft = 0;
        public int DeflectorSecondsMax = 0;

        public string AsteroidName { get; private set; }
        public BattleStationDefinition Definition { get; private set; }
        public List<Satellite> DefenseTowers { get; private set; }
        public int Level { get; private set; }
        public int UpgradeLevel => Level;

        public DateTime buildTime = DateTime.MinValue;
        public DateTime deflectorTime = DateTime.MinValue;

        private int capturingFactionId;
        private int captureProgressPoints;
        private DateTime lastCaptureProgressAt = DateTime.MinValue;
        private bool previousVulnerabilityState;

        public BattleStation(BattleStationDefinition definition, Spacemap spacemap)
            : base(spacemap, 0, definition.Position, GameManager.GetClan(0), definition.AsteroidAssetTypeId)
        {
            Definition = definition;
            AsteroidName = definition.Name;
            Name = definition.Name;
            DefenseTowers = new List<Satellite>();

            ShieldAbsorption = 0.8;
            Level = 0;
            ApplyLevelStats(true);
            Invincible = true;
            previousVulnerabilityState = Definition.IsVulnerableAt(DateTime.Now);

            Program.TickManager.AddTick(this);
        }

        public new void Tick()
        {
            var wasVulnerable = previousVulnerabilityState;
            UpdateShieldState();
            var isVulnerable = IsCurrentlyVulnerable();

            if (FactionId == 0)
                ProcessCapture();
            else
                ResetCaptureProgress();

            if (FactionId != 0 && wasVulnerable && !isVulnerable)
                HandleVulnerabilitySurvived();

            previousVulnerabilityState = isVulnerable;
        }

        public bool IsCurrentlyVulnerable()
        {
            return Definition.IsVulnerableAt(DateTime.Now);
        }

        public void HandleDestroyed(Attackable destroyer)
        {
            var destroyerName = destroyer != null ? destroyer.Name : "Unknown";
            var ownerName = GetFactionName(FactionId);

            GameManager.SendPacketToAll($"0|A|STD|Battle station {AsteroidName} on {Spacemap.Name} was destroyed by {destroyerName}. Previous owner: {ownerName}.");

            Neutralize();
            Destroyed = false;
            QueryManager.BattleStations.BattleStation(this);
            RefreshBoosterInterface();
        }

        public void HandleTowerDestroyed(Satellite tower)
        {
            if (tower == null)
                return;

            tower.Remove();
            DefenseTowers.Remove(tower);
            Spacemap.Activatables.TryRemove(tower.Id, out var removedTower);
            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(tower.GetAssetType(), tower.Id));
            RefreshBoosterInterface();
        }

        public override void Click(GameSession gameSession)
        {
            var player = gameSession.Player;
            if (player == null)
                return;

            if (FactionId == 0)
            {
                var captureState = GetCaptureState();
                var captureText = captureState.Contested
                    ? $"Neutral battle station. Capture progress is paused because multiple factions are within {Definition.CaptureRadius} units. Current progress: {GetFactionName(capturingFactionId)} {captureProgressPoints}/{GetCapturePointsRequired()}."
                    : capturingFactionId == 0 || captureProgressPoints <= 0
                        ? $"Neutral battle station. Each player within {Definition.CaptureRadius} units adds 1 capture point per second for their company, up to {GetMaxCapturePointsPerSecond()} per second. Reach {GetCapturePointsRequired()} points to capture it."
                        : $"Neutral battle station. {GetFactionName(capturingFactionId)} has {captureProgressPoints}/{GetCapturePointsRequired()} capture points.";
                player.SendPacket($"0|A|STD|{captureText}");
                return;
            }

            player.SendCommand(GetStatusCommand().writeCommand());

            var shieldState = DeflectorActive ? "Shield active" : "Vulnerable";
            player.SendPacket($"0|A|STD|Battle station owner: {GetFactionName(FactionId)}. {shieldState}. Level {GetEffectiveLevel()} (upgrade {UpgradeLevel}).");
        }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            return AssetCreateCommand.write(GetAssetType(), Name,
                    FactionId, "", Id, GetVisualDesignId(), GetVisualExpansionStage(),
                Position.X, Position.Y, 0, true, true, true, true,
                new ClanRelationModule(ClanRelationModule.NONE),
                VisualModifiers.Values.ToList());
        }

        private void ProcessCapture()
        {
            var now = DateTime.Now;

            if (lastCaptureProgressAt == DateTime.MinValue)
            {
                lastCaptureProgressAt = now;
                return;
            }

            var elapsedSeconds = (int)(now - lastCaptureProgressAt).TotalSeconds;
            if (elapsedSeconds <= 0)
                return;

            lastCaptureProgressAt = lastCaptureProgressAt.AddSeconds(elapsedSeconds);

            var captureState = GetCaptureState();
            if (captureState.Contested || captureState.ActiveFactionId == 0 || captureState.PlayerCount <= 0)
                return;

            var pointsPerSecond = Math.Min(captureState.PlayerCount, GetMaxCapturePointsPerSecond());

            for (var second = 0; second < elapsedSeconds; second++)
            {
                ApplyCaptureProgress(captureState.ActiveFactionId, pointsPerSecond);

                if (FactionId != 0)
                    break;
            }
        }

        private void Claim(int factionId)
        {
            var previousAssetType = AssetTypeId;

            FactionId = factionId;
            Clan = GameManager.GetClan(0);
            AssetTypeId = Definition.StationAssetTypeId;
            SetUpgradeLevel(1, true);

            SpawnDefenseTowers();
            ResetCaptureProgress();
            previousVulnerabilityState = IsCurrentlyVulnerable();
            UpdateShieldState(true);

            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(new AssetTypeModule(previousAssetType), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
            GameManager.SendPacketToAll($"0|A|STD|Battle station {AsteroidName} on {Spacemap.Name} was captured by {GetFactionName(FactionId)} at level 1.");

            QueryManager.BattleStations.BattleStation(this);
            RefreshBoosterInterface();
        }

        private void Neutralize()
        {
            var previousAssetType = AssetTypeId;

            ClearStationVisualEffects();
            RemoveStationSatellites();
            ResetCaptureProgress();

            FactionId = 0;
            Level = 0;
            Clan = GameManager.GetClan(0);
            AssetTypeId = Definition.AsteroidAssetTypeId;
            ApplyLevelStats(true);
            Invincible = true;
            DeflectorActive = false;
            DeflectorSecondsLeft = 0;
            DeflectorSecondsMax = 0;
            previousVulnerabilityState = Definition.IsVulnerableAt(DateTime.Now);
            RemoveVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR);

            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(new AssetTypeModule(previousAssetType), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
            RefreshBoosterInterface();
        }

        public void RefreshBoosterInterface()
        {
            if (Spacemap == null)
                return;

            foreach (var player in Spacemap.Characters.Values.OfType<Player>())
                player.BoosterManager?.Update();
        }

        private void SpawnDefenseTowers()
        {
            RemoveStationSatellites();

            foreach (var towerDefinition in Definition.Towers)
            {
                var tower = new Satellite(this, towerDefinition, Satellite.GetPosition(Position, towerDefinition.SlotId));
                DefenseTowers.Add(tower);
                Spacemap.Activatables.TryAdd(tower.Id, tower);
                GameManager.SendCommandToMap(Spacemap.Id, tower.GetAssetCreateCommand());
            }
        }

        private void RemoveDefenseTowers()
        {
            foreach (var tower in DefenseTowers.ToList())
            {
                tower.Remove();
                Spacemap.Activatables.TryRemove(tower.Id, out var removedTower);
                GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(tower.GetAssetType(), tower.Id));
            }

            DefenseTowers.Clear();
        }

        private void RemoveStationSatellites()
        {
            RemoveDefenseTowers();

            if (Spacemap == null)
                return;

            foreach (var satellite in Spacemap.Activatables.Values.OfType<Satellite>().Where(x => x != null && x.BattleStation == this).ToList())
            {
                satellite.Remove(false, true, true);
                Spacemap.Activatables.TryRemove(satellite.Id, out var removedSatellite);
                GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(satellite.GetAssetType(), satellite.Id));
            }

            EquippedStationModule.Clear();
        }

        private void ResetCaptureProgress()
        {
            capturingFactionId = 0;
            captureProgressPoints = 0;
            lastCaptureProgressAt = DateTime.MinValue;
        }

        private void ApplyCaptureProgress(int factionId, int points)
        {
            if (factionId <= 0 || points <= 0 || FactionId != 0)
                return;

            if (capturingFactionId == 0 || captureProgressPoints <= 0)
            {
                capturingFactionId = factionId;
                captureProgressPoints = Math.Min(GetCapturePointsRequired(), points);
            }
            else if (capturingFactionId == factionId)
            {
                captureProgressPoints = Math.Min(GetCapturePointsRequired(), captureProgressPoints + points);
            }
            else
            {
                captureProgressPoints = Math.Max(0, captureProgressPoints - points);

                if (captureProgressPoints == 0)
                    capturingFactionId = 0;

                return;
            }

            if (captureProgressPoints >= GetCapturePointsRequired())
                Claim(factionId);
        }

        private int GetCapturePointsRequired()
        {
            return Definition?.CapturePointsRequired > 0 ? Definition.CapturePointsRequired : 100;
        }

        private int GetMaxCapturePointsPerSecond()
        {
            return Definition?.MaxCapturePointsPerSecond > 0 ? Definition.MaxCapturePointsPerSecond : 10;
        }

        private CaptureState GetCaptureState()
        {
            var contenders = Spacemap.Characters.Values
                .OfType<Player>()
                .Where(player => !player.Destroyed && player.CurrentHitPoints > 0 && player.FactionId > 0 && player.Position.DistanceTo(Position) <= Definition.CaptureRadius)
                .GroupBy(player => player.FactionId)
                .Select(group => new { FactionId = group.Key, PlayerCount = group.Count() })
                .ToList();

            if (contenders.Count != 1)
                return new CaptureState(0, 0, contenders.Count > 1);

            return new CaptureState(contenders[0].FactionId, contenders[0].PlayerCount, false);
        }

        private void ApplyLevelStats(bool restoreCurrent)
        {
            var stats = Definition.GetCenterLevelDefinition(GetEffectiveLevel());
            MaxHitPoints = stats.MaxHitPoints > 0 ? stats.MaxHitPoints : Definition.MaxHitPoints;
            MaxShieldPoints = stats.MaxShieldPoints > 0 ? stats.MaxShieldPoints : Definition.MaxShieldPoints;

            if (restoreCurrent || CurrentHitPoints > MaxHitPoints)
                CurrentHitPoints = MaxHitPoints;
            if (restoreCurrent || CurrentShieldPoints > MaxShieldPoints)
                CurrentShieldPoints = MaxShieldPoints;

            UpdateStatus();
        }

        private void HandleVulnerabilitySurvived()
        {
            var nextLevel = Definition.GetNextLevel(UpgradeLevel);
            if (nextLevel > UpgradeLevel)
            {
                SetUpgradeLevel(nextLevel, true);
                TriggerLevelUpVisualEffect();
                GameManager.SendPacketToAll($"0|A|STD|Battle station {AsteroidName} on {Spacemap.Name} survived the vulnerability window and reached level {GetEffectiveLevel()}.");
            }
            else
            {
                GameManager.SendPacketToAll($"0|A|STD|Battle station {AsteroidName} on {Spacemap.Name} survived the vulnerability window and remains at max level.");
            }

            QueryManager.BattleStations.BattleStation(this);
        }

        private void UpdateShieldState(bool forceRefresh = false)
        {
            var shouldEnableShield = FactionId != 0 && !IsCurrentlyVulnerable();
            var shieldWasActive = DeflectorActive;

            if (!forceRefresh && shouldEnableShield == DeflectorActive)
            {
                DeflectorSecondsLeft = Definition.GetSecondsUntilStateChange(DateTime.Now);
                DeflectorSecondsMax = DeflectorSecondsLeft;
                return;
            }

            DeflectorActive = shouldEnableShield;
            Invincible = shouldEnableShield;
            DeflectorSecondsLeft = Definition.GetSecondsUntilStateChange(DateTime.Now);
            DeflectorSecondsMax = DeflectorSecondsLeft;
            deflectorTime = DateTime.Now;

            if (shouldEnableShield)
            {
                SetConstructionVisualState(false);
                AddVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR, DeflectorSecondsLeft, "", 0, true);
            }
            else
            {
                RemoveVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR);
                SetConstructionVisualState(true);
            }

            if (shouldEnableShield && !shieldWasActive)
                RestoreDestroyedTowers();
        }

        private void SetConstructionVisualState(bool constructionActive)
        {
            if (constructionActive)
                AddVisualModifier(VisualModifierCommand.CONSTRUCTION_EFFECT, 0, "", 0, true);
            else
                RemoveVisualModifier(VisualModifierCommand.CONSTRUCTION_EFFECT);

            foreach (var tower in DefenseTowers.Where(x => x != null))
            {
                if (constructionActive && !tower.Destroyed && !tower.IsDestroyedModuleState)
                    tower.AddVisualModifier(VisualModifierCommand.MODULE_INSTALL_EFFECT, 0, "", 0, true);
                else
                    tower.RemoveVisualModifier(VisualModifierCommand.MODULE_INSTALL_EFFECT);
            }
        }

        private void TriggerLevelUpVisualEffect()
        {
            _ = PlayTemporaryVisualModifier(VisualModifierCommand.MODULE_LEVEL_UP_EFFECT, 2000);

            foreach (var tower in DefenseTowers.Where(x => x != null && !x.Destroyed))
                _ = tower.PlayTemporaryVisualModifier(VisualModifierCommand.MODULE_LEVEL_UP_EFFECT, 2000);
        }

        private void ClearStationVisualEffects()
        {
            RemoveVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR);
            RemoveVisualModifier(VisualModifierCommand.CONSTRUCTION_EFFECT);
            RemoveVisualModifier(VisualModifierCommand.MODULE_LEVEL_UP_EFFECT);

            foreach (var tower in DefenseTowers.Where(x => x != null))
            {
                tower.RemoveVisualModifier(VisualModifierCommand.MODULE_INSTALL_EFFECT);
                tower.RemoveVisualModifier(VisualModifierCommand.MODULE_LEVEL_UP_EFFECT);
            }
        }

        public static string GetFactionName(int factionId)
        {
            switch (factionId)
            {
                case 1:
                    return "MMO";
                case 2:
                    return "EIC";
                case 3:
                    return "VRU";
                default:
                    return "Neutral";
            }
        }

        public int GetBoostPercentage(BoostedAttributeType boostedAttributeType)
        {
            if (FactionId == 0 || DefenseTowers == null || DefenseTowers.Count == 0)
                return 0;

            return DefenseTowers
                .Where(x => x != null && !x.Destroyed)
                .Sum(x => x.GetBoostPercentage(boostedAttributeType));
        }

        public static int GetFactionBoostPercentage(int factionId, BoostedAttributeType boostedAttributeType)
        {
            if (factionId <= 0)
                return 0;

            return GameManager.BattleStations.Values
                .Where(x => x != null && x.FactionId == factionId && !x.Destroyed)
                .Sum(x => x.GetBoostPercentage(boostedAttributeType));
        }

        public int GetEffectiveLevel()
        {
            return UpgradeLevel > 0 ? UpgradeLevel : Definition.GetMinLevel();
        }

        public bool SetUpgradeLevel(int level, bool restoreCurrent = true)
        {
            if (FactionId == 0)
                return false;

            var previousLevel = Level;
            Level = Definition.NormalizeUpgradeLevel(level);
            var levelChanged = previousLevel != Level;
            ApplyLevelStats(restoreCurrent);

            foreach (var tower in DefenseTowers.Where(x => x != null && !x.Destroyed))
            {
                tower.ApplyLevelStats(restoreCurrent);

                if (levelChanged)
                    tower.RefreshVisual(true);
            }

            if (levelChanged)
                RefreshVisual();

            QueryManager.BattleStations.BattleStation(this);

            if (levelChanged)
                RefreshBoosterInterface();

            return true;
        }

        public override int GetVisualDesignId()
        {
            var factionVisualIndex = FactionId > 0 ? FactionId - 1 : 0;
            var hullVisualIndex = Definition.GetCenterVisualIndex(GetEffectiveLevel());
            return (factionVisualIndex << 16) | (hullVisualIndex & 0xFFFF);
        }

        private static int GetClientExpansionStage(int level)
        {
            switch (level)
            {
                case 1:
                    return 1;
                case 2:
                    return 10;
                case 3:
                    return 16;
                default:
                    return Math.Max(1, level);
            }
        }

        public override int GetVisualExpansionStage()
        {
            var effectiveLevel = GetEffectiveLevel();
            var stats = Definition.GetCenterLevelDefinition(effectiveLevel);
            var visualStage = stats.ExpansionStage > 0 ? stats.ExpansionStage : GetClientExpansionStage(effectiveLevel);

            return (visualStage << 16) | (visualStage & 0xFFFF);
        }

        private void RefreshVisual()
        {
            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(GetAssetType(), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());

            if (FactionId != 0)
                BroadcastStatusCommand();
        }

        private void RestoreDestroyedTowers()
        {
            foreach (var tower in DefenseTowers.Where(x => x != null && x.IsDestroyedModuleState).ToList())
                tower.RestoreFromDestroyedState();
        }

        private class CaptureState
        {
            public int ActiveFactionId { get; private set; }
            public int PlayerCount { get; private set; }
            public bool Contested { get; private set; }

            public CaptureState(int activeFactionId, int playerCount, bool contested)
            {
                ActiveFactionId = activeFactionId;
                PlayerCount = playerCount;
                Contested = contested;
            }
        }

        public BattleStationStatusCommand GetStatusCommand()
        {
            return new BattleStationStatusCommand(
                Id,
                Id,
                Name,
                DeflectorActive,
                DeflectorSecondsLeft,
                DeflectorSecondsMax,
                GetAttackRating(),
                GetDefenceRating(),
                GetRepairRating(),
                GetHonorBoosterRating(),
                GetExperienceBoosterRating(),
                GetDamageBoosterRating(),
                GetDeflectorShieldRate(),
                Definition?.RepairPrice ?? 0,
                new EquippedModulesModule(GetStatusModules()));
        }

        private IEnumerable<Satellite> GetActiveTowers()
        {
            return DefenseTowers.Where(x => x != null && !x.Destroyed && !x.IsDestroyedModuleState);
        }

        private BattleStationLevelDefinition GetTowerStats(Satellite tower)
        {
            if (tower == null || tower.TowerDefinition == null)
                return null;

            var towerLevel = tower.UpgradeLevel > 0 ? tower.UpgradeLevel : GetEffectiveLevel();
            return tower.TowerDefinition.GetLevelDefinition(towerLevel);
        }

        private int GetAttackRating()
        {
            return GetActiveTowers()
                .Where(x => x.Type == StationModuleModule.LASER_HIGH_RANGE
                    || x.Type == StationModuleModule.LASER_MID_RANGE
                    || x.Type == StationModuleModule.LASER_LOW_RANGE
                    || x.Type == StationModuleModule.ROCKET_MID_ACCURACY
                    || x.Type == StationModuleModule.ROCKET_LOW_ACCURACY)
                .Sum(x => GetTowerStats(x)?.Damage ?? 0);
        }

        private int GetDefenceRating()
        {
            var stationDefence = Math.Max(0, MaxHitPoints) + Math.Max(0, MaxShieldPoints);
            var towerDefence = GetActiveTowers().Sum(x => Math.Max(0, x.MaxHitPoints) + Math.Max(0, x.MaxShieldPoints));
            return stationDefence + towerDefence;
        }

        private int GetRepairRating()
        {
            return GetActiveTowers()
                .Where(x => x.Type == StationModuleModule.REPAIR)
                .Sum(x => GetTowerStats(x)?.RepairAmount ?? 0);
        }

        private int GetHonorBoosterRating()
        {
            return GetBoostPercentage(BoostedAttributeType.HONOUR);
        }

        private int GetExperienceBoosterRating()
        {
            return GetBoostPercentage(BoostedAttributeType.EP);
        }

        private int GetDamageBoosterRating()
        {
            return GetBoostPercentage(BoostedAttributeType.DAMAGE);
        }

        private int GetDeflectorShieldRate()
        {
            if (DeflectorSecondsMax <= 0)
                return DeflectorActive ? 100 : 0;

            var rate = (int)Math.Round((double)Math.Max(0, DeflectorSecondsLeft) * 100 / DeflectorSecondsMax);
            return Math.Max(0, Math.Min(100, rate));
        }

        public void SendStatusCommand(Player player)
        {
            if (player == null || FactionId == 0)
                return;

            player.SendCommand(GetStatusCommand().writeCommand());
        }

        private void BroadcastStatusCommand()
        {
            if (FactionId == 0)
                return;

            GameManager.SendCommandToMap(Spacemap.Id, GetStatusCommand().writeCommand());
        }

        private List<StationModuleModule> GetStatusModules()
        {
            var modules = new List<StationModuleModule>
            {
                CreateStatusModule(0, StationModuleModule.HULL, CurrentHitPoints, MaxHitPoints, CurrentShieldPoints, MaxShieldPoints, GetEffectiveLevel(), 0, 0),
                CreateStatusModule(1, StationModuleModule.DEFLECTOR, CurrentHitPoints, MaxHitPoints, CurrentShieldPoints, MaxShieldPoints, GetEffectiveLevel(), DeflectorSecondsLeft, DeflectorSecondsMax)
            };

            foreach (var tower in DefenseTowers.Where(x => x != null))
            {
                modules.Add(CreateStatusModule(
                    tower.SlotId,
                    tower.Type,
                    tower.CurrentHitPoints,
                    tower.MaxHitPoints,
                    tower.CurrentShieldPoints,
                    tower.MaxShieldPoints,
                    tower.UpgradeLevel > 0 ? tower.UpgradeLevel : GetEffectiveLevel(),
                    tower.EmergencyRepairActive ? 1 : 0,
                    tower.EmergencyRepairActive ? 1 : 0,
                    tower.ItemId,
                    tower.OwnerId,
                    tower.Name,
                    tower.Installed ? 0 : tower.InstallationSecondsLeft,
                    tower.InstallationSecondsLeft));
            }

            return modules;
        }

        private StationModuleModule CreateStatusModule(int slotId, short type, int currentHitpoints, int maxHitpoints, int currentShield, int maxShield, int upgradeLevel, int emergencyRepairSecondsLeft, int emergencyRepairSecondsTotal, int itemId = 0, int ownerId = 0, string ownerName = "", int installationSeconds = 0, int installationSecondsLeft = 0)
        {
            return new StationModuleModule(
                Id,
                itemId,
                slotId,
                type,
                currentHitpoints,
                maxHitpoints,
                currentShield,
                maxShield,
                upgradeLevel,
                string.IsNullOrWhiteSpace(ownerName) ? ownerId.ToString() : ownerName,
                installationSeconds,
                installationSecondsLeft,
                emergencyRepairSecondsLeft,
                emergencyRepairSecondsTotal,
                0);
        }
    }
}
