using Newtonsoft.Json;
using Ow.Game;
using Ow.Game.Movements;
using Ow.Game.Objects.Players;
using Ow.Game.Objects.Players.Managers;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Game.Objects.Stations
{
    public class SatelliteBase
    {
        public int OwnerId { get; set; }
        public int ItemId { get; set; }
        public int SlotId { get; set; }
        public int DesignId { get; set; }
        public short Type { get; set; }
        public int CurrentHitPoints { get; set; }
        public int MaxHitPoints { get; set; }
        public int CurrentShieldPoints { get; set; }
        public int MaxShieldPoints { get; set; }
        public int InstallationSecondsLeft { get; set; }
        public bool Installed { get; set; }

        public SatelliteBase(int ownerId, int itemId, int slotId, int designId, short type, int currentHp, int maxHp, int currentShd, int maxShd, int installationSecondsLeft, bool installed)
        {
            OwnerId = ownerId;
            ItemId = itemId;
            SlotId = slotId;
            DesignId = designId;
            Type = type;
            CurrentHitPoints = currentHp;
            MaxHitPoints = maxHp;
            CurrentShieldPoints = currentShd;
            MaxShieldPoints = maxShd;
            InstallationSecondsLeft = installationSecondsLeft;
            Installed = installed;
        }
    }

    class Satellite : Activatable
    {
        private const int AlliedRepairRange = 700;
        private const int RepairEffectDurationMilliseconds = 2000;

        public int DesignId { get; set; }
        public BattleStation BattleStation { get; set; }
        public BattleStationTowerDefinition TowerDefinition { get; private set; }
        public int OwnerId { get; set; }
        public bool IsStaticDefenseTower { get; private set; }
        public bool IsDestroyedModuleState { get; private set; }
        public int UpgradeLevel => BattleStation?.UpgradeLevel ?? 0;

        public bool EmergencyRepairActive = false;
        public bool Installed = false;
        public int InstallationSecondsLeft = 0;

        public int ItemId { get; set; }
        public int SlotId { get; set; }
        public short Type { get; set; }

        public Satellite(BattleStation battleStation, int ownerId, string name, int designId, int itemId, int slotId, short type, Position position) : base(battleStation.Spacemap, battleStation.FactionId, position, battleStation.Clan, AssetTypeModule.SATELLITE)
        {
            ShieldAbsorption = 0.8;
            BattleStation = battleStation;
            OwnerId = ownerId;
            Name = name;
            DesignId = designId;
            ItemId = itemId;
            SlotId = slotId;
            Type = type;

            MaxHitPoints = 100000;
            CurrentHitPoints = MaxHitPoints;
            CurrentShieldPoints = 100000;
            MaxShieldPoints = 100000;

            Program.TickManager.AddTick(this);
        }

        public Satellite(BattleStation battleStation, BattleStationTowerDefinition towerDefinition, Position position)
            : base(battleStation.Spacemap, battleStation.FactionId, position, battleStation.Clan, towerDefinition.AssetTypeId)
        {
            ShieldAbsorption = 0.8;
            BattleStation = battleStation;
            OwnerId = 0;
            TowerDefinition = towerDefinition;
            Name = string.IsNullOrWhiteSpace(towerDefinition.Name) ? GetName(towerDefinition.Type) : towerDefinition.Name;
            DesignId = towerDefinition.DesignId;
            ItemId = 0;
            SlotId = towerDefinition.SlotId;
            Type = towerDefinition.Type;
            IsStaticDefenseTower = true;
            Installed = true;
            ApplyLevelStats(true);

            Program.TickManager.AddTick(this);
        }

        public DateTime installationTime = new DateTime();
        public new void Tick()
        {
            if (!Installed)
            {
                var player = GameManager.GetPlayerById(OwnerId);

                if (InstallationSecondsLeft > 0)
                {
                    if (BattleStation.AssetTypeId == AssetTypeModule.ASTEROID)
                    {
                        if (player == null || player.Position.DistanceTo(BattleStation.Position) > 700)
                            Remove(false, true, true);
                    }

                    if (installationTime.AddSeconds(1) < DateTime.Now)
                    {
                        InstallationSecondsLeft--;
                        installationTime = DateTime.Now;
                    }
                }
                else if (InstallationSecondsLeft <= 0)
                {
                    Installed = true;

                    if (BattleStation.AssetTypeId == AssetTypeModule.BATTLESTATION)
                        RemoveVisualModifier(VisualModifierCommand.BATTLESTATION_CONSTRUCTING);

                    if (player != null)
                        BattleStation.Click(player.GameSession);

                }
            }
            else if (Installed)
            {
                if (BattleStation.AssetTypeId == AssetTypeModule.BATTLESTATION)
                {
                    if (Type != StationModuleModule.DEFLECTOR && Type != StationModuleModule.HULL && Type != StationModuleModule.NONE
                        && Type != StationModuleModule.DAMAGE_BOOSTER && Type != StationModuleModule.EXPERIENCE_BOOSTER 
                        && Type != StationModuleModule.HONOR_BOOSTER && Type != StationModuleModule.REPAIR)
                    {
                        foreach (var character in Spacemap.Characters.Values)
                        {
                            if (character is Player || character is Pet)
                                Attack(character);
                        }
                    }
                    else if (Type == StationModuleModule.REPAIR)
                    {
                        RepairStationAssets();
                    }
                }
            }
        }

        public DateTime repairTime = new DateTime();
        public void RepairStationAssets()
        {
            var stats = GetCurrentLevelStats();
            if (stats == null || stats.RepairAmount <= 0)
                return;

            var repairIntervalSeconds = stats.RepairIntervalSeconds > 0 ? stats.RepairIntervalSeconds : 10;

            if (!Destroyed && repairTime.AddSeconds(repairIntervalSeconds) < DateTime.Now)
            {
                var repairedAnyTarget = false;

                if (BattleStation.LastCombatTime.AddSeconds(10) < DateTime.Now)
                    repairedAnyTarget = TryRepairTarget(BattleStation, stats.RepairAmount, VisualModifierCommand.EMERGENCY_REPAIR_EFFECT) || repairedAnyTarget;

                foreach (var tower in BattleStation.DefenseTowers.Where(x => x != null && !x.Destroyed && x.Id != Id))
                {
                    if (tower.LastCombatTime.AddSeconds(10) >= DateTime.Now)
                        continue;

                    repairedAnyTarget = TryRepairTarget(tower, stats.RepairAmount, VisualModifierCommand.EMERGENCY_REPAIR_EFFECT) || repairedAnyTarget;
                }

                foreach (var player in GetAlliedPlayersInRepairRange())
                {
                    if (player.LastCombatTime.AddSeconds(10) >= DateTime.Now)
                        continue;

                    repairedAnyTarget = TryRepairTarget(player, stats.RepairAmount, VisualModifierCommand.HEAL_EFFECT) || repairedAnyTarget;
                }

                if (repairedAnyTarget)
                    repairTime = DateTime.Now;
            }
        }

        private bool TryRepairTarget(Attackable target, int repairAmount, short visualEffect = 0)
        {
            if (target == null || repairAmount <= 0)
                return false;

            var repaired = false;

            if (target.CurrentHitPoints < target.MaxHitPoints)
            {
                target.Heal(repairAmount);
                repaired = true;
            }

            if (target.CurrentShieldPoints < target.MaxShieldPoints)
            {
                target.Heal(repairAmount, 0, HealType.SHIELD);
                repaired = true;
            }

            if (repaired && visualEffect != 0)
                _ = target.PlayTemporaryVisualModifier(visualEffect, RepairEffectDurationMilliseconds);

            return repaired;
        }

        private IEnumerable<Player> GetAlliedPlayersInRepairRange()
        {
            if (BattleStation == null || Spacemap == null || BattleStation.FactionId == 0)
                return Enumerable.Empty<Player>();

            return Spacemap.Characters.Values
                .OfType<Player>()
                .Where(player => player != null
                    && !player.Destroyed
                    && player.CurrentHitPoints > 0
                    && player.FactionId == BattleStation.FactionId
                    && player.Position.DistanceTo(BattleStation.Position) <= AlliedRepairRange)
                .ToList();
        }

        public DateTime lastAttackTime = new DateTime();
        public void Attack(Attackable target, double shieldPenetration = 0)
        {
            var currentLevelStats = GetCurrentLevelStats();
            var missProbability = currentLevelStats != null && currentLevelStats.MissProbability > 0 ? currentLevelStats.MissProbability : (Type == StationModuleModule.LASER_LOW_RANGE ? 0.1 : Type == StationModuleModule.LASER_MID_RANGE ? 0.3 : Type == StationModuleModule.LASER_HIGH_RANGE ? 0.4 : Type == StationModuleModule.ROCKET_LOW_ACCURACY ? 0.5 : Type == StationModuleModule.ROCKET_MID_ACCURACY ? 0.3 : 1.00);

            var baseDamage = currentLevelStats != null && currentLevelStats.Damage > 0 ? currentLevelStats.Damage : (Type == StationModuleModule.LASER_LOW_RANGE ? 1000 : Type == StationModuleModule.LASER_MID_RANGE ? 1400 : Type == StationModuleModule.LASER_HIGH_RANGE ? 1800 : Type == StationModuleModule.ROCKET_LOW_ACCURACY ? 1800 : Type == StationModuleModule.ROCKET_MID_ACCURACY ? 1400 : 0);
            var damage = AttackManager.RandomizeDamage(baseDamage, missProbability);

            var damageType = (Type == StationModuleModule.LASER_LOW_RANGE || Type == StationModuleModule.LASER_MID_RANGE || Type == StationModuleModule.LASER_HIGH_RANGE) ? DamageType.LASER : (Type == StationModuleModule.ROCKET_LOW_ACCURACY || Type == StationModuleModule.ROCKET_MID_ACCURACY) ? DamageType.ROCKET : DamageType.LASER;

            var cooldown = currentLevelStats != null && currentLevelStats.CooldownSeconds > 0 ? currentLevelStats.CooldownSeconds : ((Type == StationModuleModule.ROCKET_LOW_ACCURACY || Type == StationModuleModule.ROCKET_MID_ACCURACY) ? 2 : 1);

            if (target.Position.DistanceTo(Position) < GetRange())
            {
                if (!TargetDefinition(target)) return;

                if (lastAttackTime.AddSeconds(cooldown) < DateTime.Now)
                {
                    int damageShd = 0, damageHp = 0;

                    double shieldAbsorb = System.Math.Abs(target.ShieldAbsorption - shieldPenetration);

                    if (shieldAbsorb > 1)
                        shieldAbsorb = 1;

                    if ((target.CurrentShieldPoints - damage) >= 0)
                    {
                        damageShd = (int)(damage * shieldAbsorb);
                        damageHp = damage - damageShd;
                    }
                    else
                    {
                        int newDamage = damage - target.CurrentShieldPoints;
                        damageShd = target.CurrentShieldPoints;
                        damageHp = (int)(newDamage + (damageShd * shieldAbsorb));
                    }

                    if ((target.CurrentHitPoints - damageHp) < 0)
                    {
                        damageHp = target.CurrentHitPoints;
                    }

                    if (target is Player && !(target as Player).Attackable())
                    {
                        damage = 0;
                        damageShd = 0;
                        damageHp = 0;
                    }

                    if (damageType == DamageType.LASER)
                    {
                        if (target is Player && (target as Player).Storage.Sentinel)
                            damageShd -= Maths.GetPercentage(damageShd, 30);

                        if (target is Player && (target as Player).Storage.Diminisher)
                            if ((target as Player).Storage.UnderDiminisherEntity == this)
                                damageShd += Maths.GetPercentage(damage, 30);

                        var laserRunCommand = AttackLaserRunCommand.write(Id, target.Id, 0, false, false);
                        SendCommandToInRangeCharacters(laserRunCommand);
                    }
                    else if (damageType == DamageType.ROCKET)
                    {
                        var rocketRunPacket = $"0|v|{Id}|{target.Id}|H|" + 1 + "|0|1";
                        SendPacketToInRangeCharacters(rocketRunPacket);
                    }

                    if (damage == 0)
                    {
                        SendCommandToInRangeCharacters(AttackMissedCommand.write(new AttackTypeModule((short)damageType), target.Id, 1), target);

                        if (target is Player)
                            (target as Player).SendCommand(AttackMissedCommand.write(new AttackTypeModule((short)damageType), target.Id, 0));
                    }
                    else
                    {
                        var attackHitDamage = damage > damageShd ? damage : damageShd;
                        var attackHitCommand =
                                AttackHitCommand.write(new AttackTypeModule((short)damageType), Id,
                                                     target.Id, target.CurrentHitPoints,
                                                     target.CurrentShieldPoints, target.CurrentNanoHull,
                                                     attackHitDamage, false);

                        SendCommandToInRangeCharacters(attackHitCommand, target);

                        if (target is Player playerTarget)
                            playerTarget.AttackManager.QueueIncomingDamageHit(Id, damageType, attackHitDamage, false);
                    }

                    if (damageHp >= target.CurrentHitPoints || target.CurrentHitPoints <= 0)
                    {
                        if (target is Player playerTarget)
                            playerTarget.AttackManager.FlushPendingIncomingDamageHitsFromAttacker(Id);

                        target.Destroy(this, DestructionType.MISC);
                    }
                    else
                    {
                        if (target.CurrentNanoHull > 0)
                        {
                            if (target.CurrentNanoHull - damageHp < 0)
                            {
                                var nanoDamage = damageHp - target.CurrentNanoHull;
                                target.CurrentNanoHull = 0;
                                target.CurrentHitPoints -= nanoDamage;
                            }
                            else
                                target.CurrentNanoHull -= damageHp;
                        }
                        else
                            target.CurrentHitPoints -= damageHp;
                    }

                    target.CurrentShieldPoints -= damageShd;
                    target.LastCombatTime = DateTime.Now;

                    target.UpdateStatus();

                    lastAttackTime = DateTime.Now;
                }
            }
        }

        public override void Click(GameSession gameSession) { }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            return AssetCreateCommand.write(GetAssetType(), Name,
                                          FactionId, Clan.Tag, Id, DesignId, 0,
                                          Position.X, Position.Y, Clan.Id, false, true, true, true,
                                          new ClanRelationModule(clanRelationModule),
                                          VisualModifiers.Values.ToList());
        }

        public void Remove(bool deleteModule = false, bool removeList = true, bool closeUI = false)
        {
            if (IsStaticDefenseTower)
            {
                Program.TickManager.RemoveTick(this);
                return;
            }

            var player = GameManager.GetPlayerById(OwnerId);

            if (player != null)
            {
                var module = player.Storage.BattleStationModules.Where(x => x.Id == ItemId).FirstOrDefault();

                if (module != null)
                {
                    if (deleteModule)
                        player.Storage.BattleStationModules.Remove(module);
                    else
                    {
                        BattleStation.EquippedStationModule[player.Clan.Id].Remove(this);

                        if (removeList)
                        {
                            if (BattleStation.EquippedStationModule[player.Clan.Id].Count == 0)
                                BattleStation.EquippedStationModule.Remove(player.Clan.Id);
                        }

                        module.InUse = false;
                    }

                    if (closeUI)
                        player.SendCommand(OutOfBattleStationRangeCommand.write(BattleStation.Id));

                    QueryManager.SavePlayer.Modules(player);
                }
            }

            Program.TickManager.RemoveTick(this);
        }

        public int GetRange()
        {
            var currentLevelStats = GetCurrentLevelStats();
            if (currentLevelStats != null && currentLevelStats.Range > 0)
                return currentLevelStats.Range;

            return Type == StationModuleModule.LASER_LOW_RANGE ? 590 : Type == StationModuleModule.LASER_MID_RANGE ? 650 : Type == StationModuleModule.LASER_HIGH_RANGE ? 720 : Type == StationModuleModule.ROCKET_LOW_ACCURACY ? 900 : Type == StationModuleModule.ROCKET_MID_ACCURACY ? 780 : 0;
        }

        public void ApplyLevelStats(bool restoreCurrent)
        {
            if (!IsStaticDefenseTower)
                return;

            var currentLevelStats = GetCurrentLevelStats();
            if (currentLevelStats == null)
                return;

            MaxHitPoints = currentLevelStats.MaxHitPoints;
            MaxShieldPoints = currentLevelStats.MaxShieldPoints;

            if (restoreCurrent || CurrentHitPoints > MaxHitPoints)
                CurrentHitPoints = MaxHitPoints;
            if (restoreCurrent || CurrentShieldPoints > MaxShieldPoints)
                CurrentShieldPoints = MaxShieldPoints;

            if (!IsDestroyedModuleState)
                DesignId = GetCurrentDesignId();

            UpdateStatus();
        }

        public int GetBoostPercentage(BoostedAttributeType boostedAttributeType)
        {
            if (!IsStaticDefenseTower || BattleStation == null || BattleStation.FactionId == 0)
                return 0;

            if (boostedAttributeType == BoostedAttributeType.HONOUR && Type == StationModuleModule.HONOR_BOOSTER)
                return GetCurrentLevelStats()?.BoostPercent ?? 0;

            if (boostedAttributeType == BoostedAttributeType.EP && Type == StationModuleModule.EXPERIENCE_BOOSTER)
                return GetCurrentLevelStats()?.BoostPercent ?? 0;

            return 0;
        }

        private BattleStationLevelDefinition GetCurrentLevelStats()
        {
            return IsStaticDefenseTower && BattleStation != null && BattleStation.Definition != null
                ? BattleStation.Definition.Towers.FirstOrDefault(x => x.SlotId == SlotId)?.GetLevelDefinition(BattleStation.GetEffectiveLevel())
                : null;
        }

        private int GetCurrentDesignId()
        {
            var levelStats = GetCurrentLevelStats();
            if (levelStats != null && levelStats.DesignId > 0)
                return levelStats.DesignId;

            return TowerDefinition?.DesignId ?? DesignId;
        }

        public void EnterDestroyedState()
        {
            if (!IsStaticDefenseTower || TowerDefinition == null)
                return;

            RemoveVisualModifier(VisualModifierCommand.MODULE_INSTALL_EFFECT);
            RemoveVisualModifier(VisualModifierCommand.MODULE_LEVEL_UP_EFFECT);
            IsDestroyedModuleState = true;
            Type = StationModuleModule.DESTROYED;
            DesignId = TowerDefinition.DestroyedDesignId;
            CurrentHitPoints = 0;
            CurrentShieldPoints = 0;
            Destroyed = true;
            UpdateStatus();

            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(GetAssetType(), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
        }

        public void RestoreFromDestroyedState()
        {
            if (!IsStaticDefenseTower || TowerDefinition == null || !IsDestroyedModuleState)
                return;

            IsDestroyedModuleState = false;
            Destroyed = false;
            Type = TowerDefinition.Type;
            DesignId = GetCurrentDesignId();
            ApplyLevelStats(true);

            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(GetAssetType(), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
        }

        public void RefreshVisual()
        {
            if (!IsStaticDefenseTower || IsDestroyedModuleState)
                return;

            var updatedDesignId = GetCurrentDesignId();
            if (DesignId == updatedDesignId)
                return;

            DesignId = updatedDesignId;
            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(GetAssetType(), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
        }

        public static string GetName(short type)
        {
            return type == StationModuleModule.REPAIR ? "REPM-1" : type == StationModuleModule.LASER_HIGH_RANGE ? "LTM-HR" : type == StationModuleModule.LASER_MID_RANGE ? "LTM-MR" : type == StationModuleModule.LASER_LOW_RANGE ? "LTM-LR" : type == StationModuleModule.ROCKET_LOW_ACCURACY ? "RAM-LA" : type == StationModuleModule.ROCKET_MID_ACCURACY ? "RAM-MA" : type == StationModuleModule.HONOR_BOOSTER ? "HONM-1" : type == StationModuleModule.DAMAGE_BOOSTER ? "DMGM-1" : type == StationModuleModule.EXPERIENCE_BOOSTER ? "XPM-1" : "";
        }

        public static Position GetPosition(Position center, int slotId)
        {
            return slotId == 9 ? new Position(center.X - 171, center.Y - 236) : slotId == 2 ? new Position(center.X + 170, center.Y - 235) : slotId == 3 ? new Position(center.X + 412, center.Y - 98) : slotId == 4 ? new Position(center.X + 412, center.Y + 97) : slotId == 5 ? new Position(center.X + 170, center.Y + 236) : slotId == 6 ? new Position(center.X - 171, center.Y + 235) : slotId == 7 ? new Position(center.X - 413, center.Y + 97) : slotId == 8 ? new Position(center.X - 413, center.Y - 98) : center;
        }
    }
}
