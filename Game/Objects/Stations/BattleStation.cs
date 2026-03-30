using Ow.Game;
using Ow.Game.Movements;
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
        private DateTime captureStartedAt = DateTime.MinValue;
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
                ResetCapture();

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
        }

        public void HandleTowerDestroyed(Satellite tower)
        {
            if (tower == null)
                return;

            tower.Remove();
            DefenseTowers.Remove(tower);
            Spacemap.Activatables.TryRemove(tower.Id, out var removedTower);
            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(tower.GetAssetType(), tower.Id));
        }

        public override void Click(GameSession gameSession)
        {
            var player = gameSession.Player;
            if (player == null)
                return;

            if (FactionId == 0)
            {
                var captureText = capturingFactionId == 0
                    ? $"Neutral battle station. Stay within {Definition.CaptureRadius} units for {Definition.CaptureSeconds} seconds to capture it for your company."
                    : $"Neutral battle station. {GetFactionName(capturingFactionId)} is capturing it.";
                player.SendPacket($"0|A|STD|{captureText}");
                return;
            }

            var shieldState = DeflectorActive ? "Shield active" : "Vulnerable";
            player.SendPacket($"0|A|STD|Battle station owner: {GetFactionName(FactionId)}. {shieldState}. Level {GetEffectiveLevel()} (upgrade {UpgradeLevel}).");
        }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            return AssetCreateCommand.write(GetAssetType(), Name,
                FactionId, "", Id, GetCurrentDesignId(), GetCurrentExpansionStage(),
                Position.X, Position.Y, 0, true, true, true, true,
                new ClanRelationModule(ClanRelationModule.NONE),
                VisualModifiers.Values.ToList());
        }

        private void ProcessCapture()
        {
            var contenders = Spacemap.Characters.Values
                .OfType<Player>()
                .Where(player => !player.Destroyed && player.CurrentHitPoints > 0 && player.Position.DistanceTo(Position) <= Definition.CaptureRadius)
                .GroupBy(player => player.FactionId)
                .Where(group => group.Key > 0 && group.Count() >= Definition.MinPlayersToCapture)
                .ToList();

            if (contenders.Count != 1)
            {
                ResetCapture();
                return;
            }

            var contenderFactionId = contenders[0].Key;
            if (capturingFactionId != contenderFactionId)
            {
                capturingFactionId = contenderFactionId;
                captureStartedAt = DateTime.Now;
                return;
            }

            if (captureStartedAt != DateTime.MinValue && captureStartedAt.AddSeconds(Definition.CaptureSeconds) <= DateTime.Now)
                Claim(contenderFactionId);
        }

        private void Claim(int factionId)
        {
            var previousAssetType = AssetTypeId;

            FactionId = factionId;
            Clan = GameManager.GetClan(0);
            AssetTypeId = Definition.StationAssetTypeId;
            SetUpgradeLevel(1, true);

            SpawnDefenseTowers();
            ResetCapture();
            previousVulnerabilityState = IsCurrentlyVulnerable();
            UpdateShieldState(true);

            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(new AssetTypeModule(previousAssetType), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
            GameManager.SendPacketToAll($"0|A|STD|Battle station {AsteroidName} on {Spacemap.Name} was captured by {GetFactionName(FactionId)} at level 1.");

            QueryManager.BattleStations.BattleStation(this);
        }

        private void Neutralize()
        {
            var previousAssetType = AssetTypeId;

            RemoveDefenseTowers();
            ResetCapture();

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
        }

        private void SpawnDefenseTowers()
        {
            RemoveDefenseTowers();

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

        private void ResetCapture()
        {
            capturingFactionId = 0;
            captureStartedAt = DateTime.MinValue;
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
            if (UpgradeLevel < Definition.GetMaxLevel())
            {
                SetUpgradeLevel(UpgradeLevel + 1, true);
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
                AddVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR, DeflectorSecondsLeft, "", 0, true);
            else
                RemoveVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR);

            if (shouldEnableShield && !shieldWasActive)
                RestoreDestroyedTowers();
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
            return UpgradeLevel <= 0 ? 1 : UpgradeLevel;
        }

        public bool SetUpgradeLevel(int level, bool restoreCurrent = true)
        {
            if (FactionId == 0)
                return false;

            var previousLevel = Level;
            var maxLevel = Definition.GetMaxLevel();
            if (level < 1)
                level = 1;
            else if (level > maxLevel)
                level = maxLevel;

            Level = level;
            ApplyLevelStats(restoreCurrent);

            foreach (var tower in DefenseTowers.Where(x => x != null && !x.Destroyed))
            {
                tower.ApplyLevelStats(restoreCurrent);
                tower.RefreshVisual();
            }

            if (previousLevel != Level)
                RefreshVisual();

            QueryManager.BattleStations.BattleStation(this);
            return true;
        }

        private int GetCurrentDesignId()
        {
            return Definition.GetCenterLevelDefinition(GetEffectiveLevel()).DesignId;
        }

        private int GetCurrentExpansionStage()
        {
            var stats = Definition.GetCenterLevelDefinition(GetEffectiveLevel());
            return stats.ExpansionStage > 0 ? stats.ExpansionStage : Math.Max(0, GetEffectiveLevel() - 1);
        }

        private void RefreshVisual()
        {
            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(GetAssetType(), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
        }

        private void RestoreDestroyedTowers()
        {
            foreach (var tower in DefenseTowers.Where(x => x != null && x.IsDestroyedModuleState).ToList())
                tower.RestoreFromDestroyedState();
        }
    }
}
