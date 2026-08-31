using Ow.Game;
using Ow.Game.Objects.Players.Managers;
using Ow.Game.Movements;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using Ow.Game.Events;
using Ow.Game.Objects.Stations;
using Ow.Game.Objects.Players;
using Ow.Net.netty;
using System.Threading.Tasks;
using Ow.Managers.MySQLManager;
using Newtonsoft.Json;
using Ow.Game.Ticks;
using System.Globalization;

namespace Ow.Game.Objects
{
    class Player : Character
    {
        private const bool EnableModernOreRefinementSync = true;
        private const int ShotOreRefinementSyncIntervalSeconds = 5;
        private const int TimedOreRefinementSyncIntervalSeconds = 60;

        private DateTime nextShotOreRefinementSyncAt = DateTime.MinValue;
        private DateTime nextTimedOreRefinementSyncAt = DateTime.MinValue;

        public string PetName { get; set; }
        public int PetXp { get; set; }
        public int PetLevel { set; get; }
        public int RankId { get; set; }
        public int WarRank { get; set; }
        public bool Premium { get; set; }
        public string Title { get; set; }
        public byte RepairBotId = 3;

        public bool AutoRocketCPU = false;
        public bool AutoRocketLauncherCPU = false;
        public bool CloakCPU = false;

        public int Level
        {
            get
            {
                short lvl = 1;
                long expNext = 10000;

                while (Data.experience >= expNext)
                {
                    expNext *= 2;
                    lvl++;
                }
                
                return lvl;
            }
            set
            {
                Level = value;
            }
        }

        public int CurrentInRangePortalId = -1;
        public int CurrentShieldConfig1 { get; set; }
        public int CurrentShieldConfig2 { get; set; }
        public double CurrentShieldAbsConfig1 { get; set; }
        public double CurrentShieldAbsConfig2 { get; set; }
        public int CurrentConfig { get; set; }

        public int Score = 0;

        public int EquipExpansion
        {
            get { return Expansion; }
            set { Expansion = PlayerShipExtension.NormalizeLevel(Ship, value); }
        }

        public DateTime UnderEmp = DateTime.Now;

        public Boolean FriendlyMap = true;

        public SettingsBase Settings = new SettingsBase();
        public DestructionsBase Destructions { get; set; }
        public EquipmentBase Equipment { get; set; }
        public DataBase Data { get; set; }
        public SkillTreeBase SkillTree = new SkillTreeBase();
        public Group Group { get; set; }
        public Pet Pet { get; set; }
        public AttackManager AttackManager { get; set; }
        public SettingsManager SettingsManager { get; set; }
        public DroneManager DroneManager { get; set; }
        public CpuManager CpuManager { get; set; }
        public TechManager TechManager { get; set; }
        public SkillManager SkillManager { get; set; }
        public BoosterManager BoosterManager { get; set; }
        public AchievementManager Achievements { get; set; }
        public Quests Quests { get; set; }
        public LastPosition LastPosition { get; set; }
        public ShipStatus ShipStatus { get; set; }
        public Ammo Ammo = new Ammo();

        public int Prometium = 0;
        public int Endurium = 0;
        public int Terbium = 0;
        public int Prometid = 0;
        public int Duranium = 0;
        public int Promerium = 0;
        public int Xenomit = 0;
        public int Seprom = 0;
        public int Palladium = 0;

        public int CargoCapacity
        {
            get
            {
                var baseCargo = Ship != null && Ship.Cargo > 0 ? Ship.Cargo : 3000;
                var logisticsCargo = baseCargo + Maths.GetPercentage(baseCargo, GetSkillPercentage("Logistics"));
                return logisticsCargo + (CpuManager?.GetCargoCapacityBonus(baseCargo) ?? 0);
            }
        }
        public int CargoInUse => Math.Max(0, Prometium + Endurium + Terbium + Prometid + Duranium + Promerium + Xenomit + Seprom + Palladium);
        public int FreeCargo => Math.Max(0, CargoCapacity - CargoInUse);

        public int GetBoostedExperience(int experience, bool includeShipBoost = false, bool includeNpcSkill = false)
        {
            var value = includeShipBoost ? Ship.GetExperienceBoost(experience) : experience;

            value += Maths.GetPercentage(value, BoosterManager.GetPercentage(BoostedAttributeType.EP));
            value += Maths.GetPercentage(value, BattleStation.GetPlayerBoostPercentage(this, BoostedAttributeType.EP));

            if (includeNpcSkill)
                value += Maths.GetPercentage(value, GetSkillPercentage("Tactics"));

            return value;
        }

        public override int RenderRange => Storage != null && Storage.SpearheadRecon ? 4000 : base.RenderRange;

        public bool fulllf3 = true;

        public int equipedlasercount = 0;

        public void LoadCargo(string cargoJson = null)
        {
            Cargo cargo = null;
            if (!string.IsNullOrWhiteSpace(cargoJson))
            {
                try { cargo = JsonConvert.DeserializeObject<Cargo>(cargoJson); } catch { cargo = null; }
            }

            if (cargo == null && Data != null && Data.cargo != null)
                cargo = Data.cargo;

            if (cargo == null)
                cargo = new Cargo();

            Prometium = Math.Max(0, cargo.Prometium);
            Endurium = Math.Max(0, cargo.Endurium);
            Terbium = Math.Max(0, cargo.Terbium);
            Prometid = Math.Max(0, cargo.Prometid);
            Duranium = Math.Max(0, cargo.Duranium);
            Promerium = Math.Max(0, cargo.Promerium);
            Xenomit = Math.Max(0, cargo.Xenomit);
            Seprom = Math.Max(0, cargo.Seprom);
            Palladium = Math.Max(0, cargo.Palladium);
        }

        public string SerializeCargo()
        {
            var cargo = new Cargo
            {
                Prometium = Math.Max(0, Prometium),
                Endurium = Math.Max(0, Endurium),
                Terbium = Math.Max(0, Terbium),
                Prometid = Math.Max(0, Prometid),
                Duranium = Math.Max(0, Duranium),
                Promerium = Math.Max(0, Promerium),
                Xenomit = Math.Max(0, Xenomit),
                Seprom = Math.Max(0, Seprom),
                Palladium = Math.Max(0, Palladium)
            };

            return JsonConvert.SerializeObject(cargo);
        }

        private OreUpgradeBase GetOreUpgrade(short refinementType)
        {
            if (Data == null)
                return null;

            switch (refinementType)
            {
                case RefinementTypeModule.LASER: return Data.laserUpgrade;
                case RefinementTypeModule.ROCKET: return Data.rocketUpgrade;
                case RefinementTypeModule.DRIVING: return Data.drivingUpgrade;
                case RefinementTypeModule.SHIELD: return Data.shieldUpgrade;
                default: return null;
            }
        }

        private static int GetOreUpgradePercentage(int resource, short refinementType)
        {
            var ore = (Ores)resource;
            switch (refinementType)
            {
                case RefinementTypeModule.LASER:
                    return ore == Ores.Prometid ? 15 :
                           ore == Ores.Promerium ? 30 :
                           ore == Ores.Seprom ? 60 : 0;
                case RefinementTypeModule.ROCKET:
                    return ore == Ores.Prometid ? 15 :
                           ore == Ores.Promerium ? 30 :
                           ore == Ores.Seprom ? 60 : 0;
                case RefinementTypeModule.DRIVING:
                    return ore == Ores.Duranium ? 10 :
                           ore == Ores.Promerium ? 20 : 0;
                case RefinementTypeModule.SHIELD:
                    return ore == Ores.Duranium ? 10 :
                           ore == Ores.Promerium ? 20 :
                           ore == Ores.Seprom ? 40 : 0;
                default:
                    return 0;
            }
        }

        private int GetTimedOreUpgradePercentage(short refinementType)
        {
            var upgrade = GetOreUpgrade(refinementType);
            if (upgrade == null || upgrade.resource < 0 || upgrade.expiresAt <= DateTime.UtcNow)
                return 0;

            return GetOreUpgradePercentage(upgrade.resource, refinementType);
        }

        public int GetShotOreUpgradePercentage(short refinementType)
        {
            var upgrade = GetOreUpgrade(refinementType);
            if (upgrade == null || upgrade.resource < 0 || upgrade.amount <= 0)
                return 0;

            return GetOreUpgradePercentage(upgrade.resource, refinementType);
        }

        public void ConsumeShotOreUpgrade(short refinementType, int shots)
        {
            if (shots <= 0)
                return;

            var upgrade = GetOreUpgrade(refinementType);
            if (upgrade == null ||
                (refinementType != RefinementTypeModule.LASER && refinementType != RefinementTypeModule.ROCKET) ||
                upgrade.amount <= 0)
                return;

            upgrade.amount = Math.Max(0, upgrade.amount - shots);
            if (upgrade.amount == 0)
            {
                upgrade.resource = -1;
                upgrade.expiresAt = DateTime.MinValue;
            }

            // The server amount is consumed above, but the client only changes
            // the refinement counters when it receives the complete state again.
            var now = DateTime.UtcNow;
            if (GameSession != null &&
                (upgrade.amount == 0 || now >= nextShotOreRefinementSyncAt))
            {
                SendModernOreRefinementState(RefinementTypeModule.LASER, RefinementTypeModule.ROCKET);
                nextShotOreRefinementSyncAt = now.AddSeconds(ShotOreRefinementSyncIntervalSeconds);
            }
        }

        private bool ExpireOreUpgrades()
        {
            if (Data == null)
                return false;

            var changed = false;
            foreach (var upgrade in new[] { Data.drivingUpgrade, Data.shieldUpgrade })
            {
                if (upgrade != null && upgrade.resource >= 0 && upgrade.expiresAt <= DateTime.UtcNow)
                {
                    upgrade.resource = -1;
                    upgrade.amount = 0;
                    upgrade.expiresAt = DateTime.MinValue;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsUpgradeResourceAllowed(short refinementType, Ores oreType)
        {
            switch (refinementType)
            {
                case RefinementTypeModule.LASER:
                    return oreType == Ores.Prometid || oreType == Ores.Promerium || oreType == Ores.Seprom;
                case RefinementTypeModule.ROCKET:
                    return oreType == Ores.Prometid || oreType == Ores.Promerium || oreType == Ores.Seprom;
                case RefinementTypeModule.DRIVING:
                    return oreType == Ores.Duranium || oreType == Ores.Promerium;
                case RefinementTypeModule.SHIELD:
                    return oreType == Ores.Duranium || oreType == Ores.Promerium || oreType == Ores.Seprom;
                default:
                    return false;
            }
        }

        private static string GetUpgradeName(short refinementType)
        {
            switch (refinementType)
            {
                case RefinementTypeModule.LASER: return "laser damage";
                case RefinementTypeModule.ROCKET: return "rocket damage";
                case RefinementTypeModule.DRIVING: return "speed";
                case RefinementTypeModule.SHIELD: return "shield";
                default: return "upgrade";
            }
        }



        public Player(int id, string name, Clan clan, int factionId, int rankId, int warRank, Ship ship)
                     : base(id, name, factionId, ship, new Position(0, 0), null, clan, PlayerShipExtension.GetDefaultLevel(ship))
        {
            Name = name;
            Clan = clan;
            FactionId = factionId;
            RankId = rankId;
            WarRank = warRank;
            LoadAmmo();
            InitiateManagers();
            Achievements = new AchievementManager(this);
            Quests = new Quests(this);

            MaxNanoHull = ship.BaseHitpoints;
        }

        public void InitiateManagers()
        {
            DroneManager = new DroneManager(this);
            AttackManager = new AttackManager(this);
            TechManager = new TechManager(this);
            SkillManager = new SkillManager(this);
            CpuManager = new CpuManager(this);
            SettingsManager = new SettingsManager(this);
            BoosterManager = new BoosterManager(this);
        }

        public int GetPetShip(int level)
        {
            return level > 1 && level < 4 ? 12 : level > 4 && level < 7 ? 13 : level > 7 && level < 10 ? 14 : level > 10 && level < 13 ? 15 : 22;
        }

        public int GetPetExpansion(int level)
        {
            return level == 2 ? 2 : level == 3 ? 3 : level == 5 ? 2 : level == 6 ? 3 : level == 8 ? 2 : level == 9 ? 3 : level == 11 ? 2 : level == 12 ? 3 : level == 14 ? 2 : level == 15 ? 3 : 1;
        }

        public override void Tick()
        {
            Movement.ActualPosition(this);
            if (ExpireOreUpgrades() && GameSession != null)
            {
                UpdateStatus();
                SendModernOreState();
                SendModernOreRefinementState();
                QueryManager.SavePlayer.Information(this);
            }
            else if (GameSession != null && HasActiveTimedOreUpgrades() &&
                     DateTime.UtcNow >= nextTimedOreRefinementSyncAt)
            {
                SendModernOreRefinementState(RefinementTypeModule.DRIVING, RefinementTypeModule.SHIELD);
                nextTimedOreRefinementSyncAt = DateTime.UtcNow.AddSeconds(TimedOreRefinementSyncIntervalSeconds);
            }
            CheckHitpointsRepair();
            CheckShieldPointsRepair();
            CheckRadiation();
            AttackManager.LaserAttack();
            AttackManager.RocketLauncher.Tick();
            RefreshAttackers();
            Logout();

            Storage.Tick();
            DroneManager.Tick();
            TechManager.Tick();
            SkillManager.Tick();
            BoosterManager.Tick();
            Quests?.Tick();
            AttackManager.FlushPendingDamageHits();
            FlushPendingDataChanges();
        }

        public DateTime lastHpRepairTime = new DateTime();
        private void CheckHitpointsRepair()
        {
            if (CurrentHitPoints >= MaxHitPoints || AttackingOrUnderAttack() || Moving || CpuManager == null || !CpuManager.CanUseRepairBot())
            {
                if (Storage.RepairBotActivated)
                    RepairBot(false);
                return;
            }

            if (lastHpRepairTime.AddSeconds(1) >= DateTime.Now) return;

            if (!Storage.RepairBotActivated)
                RepairBot(true);

            //int repairHitpoints = MaxHitPoints / 40;
            int repairHitpoints = RepairBotId == 1 ? 1000 : RepairBotId == 2 ? 2000 : RepairBotId == 3 ? 4000: RepairBotId == 4 ? 6000 : 500;
            repairHitpoints += Maths.GetPercentage(repairHitpoints, BoosterManager.GetPercentage(BoostedAttributeType.REPAIR));
            repairHitpoints += Maths.GetPercentage(repairHitpoints, GetSkillPercentage("Engineering"));

            Heal(repairHitpoints);

            lastHpRepairTime = DateTime.Now;
        }

        public DateTime lastShieldRepairTime = new DateTime();
        private void CheckShieldPointsRepair()
        {
            if (LastCombatTime.AddSeconds(10) >= DateTime.Now || lastShieldRepairTime.AddSeconds(1) >= DateTime.Now ||
                CurrentShieldPoints >= MaxShieldPoints || Settings.InGameSettings.selectedFormation == DroneManager.MOTH_FORMATION
                || Settings.InGameSettings.selectedFormation == DroneManager.WHEEL_FORMATION) return;

            int repairShield = MaxShieldPoints / 25;
            repairShield += Maths.GetPercentage(repairShield, BoosterManager.GetPercentage(BoostedAttributeType.SHIELDRECHARGE));
            CurrentShieldPoints += repairShield;
            UpdateStatus();

            lastShieldRepairTime = DateTime.Now;
        }

        public DateTime lastRadiationDamageTime = new DateTime();
        public DateTime RadiationEnterTime = new DateTime();
        public void CheckRadiation()
        {
            if (Storage.Jumping || Storage.invincibilityEffectTime.AddSeconds(5) >= DateTime.Now) return;

            if (!Storage.IsInRadiationZone)
            {
                RadiationEnterTime = new DateTime();
                return;
            }

            if (RadiationEnterTime == new DateTime())
                RadiationEnterTime = DateTime.Now;

            if (RadiationEnterTime.AddSeconds(3) > DateTime.Now) return;

            if (lastRadiationDamageTime.AddSeconds(1) >= DateTime.Now) return;

            double distanceOutside = 0;
            try
            {
                if (Spacemap != null && Spacemap.Limits != null && Spacemap.Limits.Length == 2 && Spacemap.Limits[0] != null && Spacemap.Limits[1] != null)
                {
                    var left = Spacemap.Limits[0].X;
                    var top = Spacemap.Limits[0].Y;
                    var right = Spacemap.Limits[1].X;
                    var bottom = Spacemap.Limits[1].Y;

                    double outsideX = 0;
                    double outsideY = 0;

                    if (Position.X < left) outsideX = left - Position.X;
                    else if (Position.X > right) outsideX = Position.X - right;

                    if (Position.Y < top) outsideY = top - Position.Y;
                    else if (Position.Y > bottom) outsideY = Position.Y - bottom;

                    distanceOutside = Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
                }
            }
            catch { distanceOutside = 0; }

            const double DISTANCE_STEP = 1000.0;
            const double BASE_PERCENT = 0.01;
            const double MAX_PERCENT = 0.10;

            double exponent = Math.Floor(distanceOutside / DISTANCE_STEP);
            double percent = BASE_PERCENT * Math.Pow(2, exponent);
            if (percent > MAX_PERCENT) percent = MAX_PERCENT;

            int damage = (int)Math.Ceiling(MaxHitPoints * percent);

            AttackManager.Damage(this, this, DamageType.RADIATION, damage, true, true, false);
            lastRadiationDamageTime = DateTime.Now;
        }

        public void SetSpeedBoost(int speed)
        {
            Storage.SpeedBoost = speed;
            SendCommand(SetSpeedCommand.write(Speed, Speed));
        }

        public void RepairBot(bool activated)
        {
            Storage.RepairBotActivated = activated;
            SendCommand(GetBeaconCommand());
        }

        public void SetShieldSkillActivated(bool pShieldSkillActivated)
        {
            Storage.ShieldSkillActivated = pShieldSkillActivated;

            if (pShieldSkillActivated)
                SendCommand(AttributeSkillShieldUpdateCommand.write(1, 1, 0));
            else
                SendCommand(AttributeSkillShieldUpdateCommand.write(0, 0, 0));
        }

        public override int Speed
        {
            get
            {
                var value = CurrentConfig == 1 ? Equipment.Configs.Config1Speed : Equipment.Configs.Config2Speed;

                switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
                {
                    case DroneManager.DOME_FORMATION:
                        value -= Maths.GetPercentage(value, 50);
                        break;
                    case DroneManager.CRAB_FORMATION:
                        value -= Maths.GetPercentage(value, 15);
                        break;
                    case DroneManager.BAT_FORMATION:
                        value -= Maths.GetPercentage(value, 15);
                        break;
                    case DroneManager.RING_FORMATION:
                        value -= Maths.GetPercentage(value, 5);
                        break;
                    case DroneManager.DRILL_FORMATION:
                        value -= Maths.GetPercentage(value, 5);
                        break;
                    case DroneManager.WHEEL_FORMATION:
                        value += Maths.GetPercentage(value, 5);
                        break;
                }

                if (Storage.underDCR_250)
                    value -= Maths.GetPercentage(value, 30);

                if (Storage.underSLM_01)
                    value -= Maths.GetPercentage(value, 50);

                if (Storage.underR_IC3)
                    value -= value;

                if (Storage.Lightning)
                    value += Maths.GetPercentage(value, 30);

                if (Storage.CitadelFortify)
                    return 200;

                if (Storage.CitadelTravel)
                    return TimeManager.CITADEL_TRAVEL_SPEED;

                value += Maths.GetPercentage(value, GetTimedOreUpgradePercentage(RefinementTypeModule.DRIVING));
                value += Storage.SpeedBoost;

                return value;
            }
        }

        public override int MaxHitPoints
        {
            get
            {
                var value = CurrentConfig == 1 ? Equipment.Configs.Config1Hitpoints : Equipment.Configs.Config2Hitpoints;
                value = (Ship.Id == Ship.LEONOV && FriendlyMap != true) ? value : value + 96000;
                value += Maths.GetPercentage(value, BoosterManager.GetPercentage(BoostedAttributeType.MAXHP));
                value += GetSkillPercentage("Ship Hull");


                switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
                {
                    case DroneManager.CHEVRON_FORMATION:
                        value -= Maths.GetPercentage(value, 20);
                        break;
                    case DroneManager.DIAMOND_FORMATION:
                        value -= Maths.GetPercentage(value, 30);
                        break;
                    case DroneManager.MOTH_FORMATION:
                    case DroneManager.HEART_FORMATION:
                        value += Maths.GetPercentage(value, 20);
                        break;
                }
                value = Ship.GetHitPointsBoost(value);
                return value;
            }
        }

        public double RocketSpeed
        {
            get
            {
                var value = Premium ? 1.0 : 3.0;

                switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
                {
                    case DroneManager.DOME_FORMATION:
                        value -= 0.25;
                        break;
                    case DroneManager.RING_FORMATION:
                        value += 0.25;
                        break;
                }

                return value;
            }
        }

        public double RocketLauncherSpeed
        {
            get
            {
                var value = 1.0;

                switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
                {
                    case DroneManager.DOME_FORMATION:
                        value -= 0.25;
                        break;
                    case DroneManager.STAR_FORMATION:
                        value += 0.33;
                        break;
                    case DroneManager.RING_FORMATION:
                        value += 0.25;
                        break;
                }

                return value;
            }
        }

        public override int CurrentShieldPoints
        {
            get
            {
                var value = CurrentConfig == 1 ? CurrentShieldConfig1 : CurrentShieldConfig2;
                return value;
            }
            set
            {
                if (CurrentConfig == 1)
                    CurrentShieldConfig1 = value;
                else
                    CurrentShieldConfig2 = value;
            }
        }

        public override int MaxShieldPoints
        {
            get
            {
                var value = CurrentConfig == 1 ? Equipment.Configs.Config1Shield : Equipment.Configs.Config2Shield;
                if (Ship.Id == Ship.LEONOV && FriendlyMap == true)
                    value += CurrentConfig == 1 ? Equipment.Configs.LeonovConfig1Shield : Equipment.Configs.LeonovConfig2Shield;
                value += Maths.GetPercentage(value, GetTimedOreUpgradePercentage(RefinementTypeModule.SHIELD));
                value += Maths.GetPercentage(value, BoosterManager.GetPercentage(BoostedAttributeType.SHIELD));
                value += Maths.GetPercentage(value, GetSkillPercentage("Shield Engineering"));

                switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
                {
                    case DroneManager.TURTLE_FORMATION:
                        value += Maths.GetPercentage(value, 10);
                        break;
                    case DroneManager.RING_FORMATION:
                        value += Maths.GetPercentage(value, 85);
                        break;
                    case DroneManager.DRILL_FORMATION:
                        value -= Maths.GetPercentage(value, 25);
                        break;
                    case DroneManager.DOME_FORMATION:
                        value += Maths.GetPercentage(value, 30);
                        break;
                    case DroneManager.HEART_FORMATION:
                        value += Maths.GetPercentage(value, 10);
                        break;
                    case DroneManager.DOUBLE_ARROW_FORMATION:
                        value -= Maths.GetPercentage(value, 20);
                        break;
                }
                value = Ship.GetShieldPointsBoost(value);
                return value;
            }
        }

        public override double ShieldAbsorption
        {
            get
            {
                var value = CurrentConfig == 1 ? CurrentShieldAbsConfig1/100 : CurrentShieldAbsConfig2/100;
                value += GetSkillPercentage("Shield Mechanics") / 100.0;
                switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
                {
                    case DroneManager.CRAB_FORMATION:
                        value += 0.2;
                        break;
                    case DroneManager.BARRAGE_FORMATION:
                        value -= 0.15;
                        break;
                }
                return value;
            }
        }

        public override double ShieldPenetration
        {
            get
            {
                switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
                {
                    case DroneManager.MOTH_FORMATION:
                        return 0.2; // 0.2
                    case DroneManager.DOUBLE_ARROW_FORMATION:
                        return 0.1;
                    case DroneManager.PINCER_FORMATION:
                        return -0.1;
                    default:
                        return 0;
                }

            }
        }

        public override int Damage
        {
            get
            {
                var value = CurrentConfig == 1 ? Equipment.Configs.Config1Damage : Equipment.Configs.Config2Damage;
                if (Ship.Id == Ship.LEONOV && GetLeonovEffect(Spacemap.Id,FactionId) == true)
                    value += CurrentConfig == 1 ? Equipment.Configs.LeonovConfig1Damage : Equipment.Configs.LeonovConfig2Damage;
                value += Maths.GetPercentage(value, GetShotOreUpgradePercentage(RefinementTypeModule.LASER));
                value += Maths.GetPercentage(value, BoosterManager.GetPercentage(BoostedAttributeType.DAMAGE));
                value += Maths.GetPercentage(value, BattleStation.GetPlayerBoostPercentage(this, BoostedAttributeType.DAMAGE));

                if (Selected != null && Selected.FactionId != 0)
                {
                    value += Maths.GetPercentage(value, GetSkillPercentage("Bounty Hunter"));
                }

                if (Selected is Npc)
                {
                    value += Maths.GetPercentage(value, GetSkillPercentage("Alien Hunter"));
                }

                switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
                {
                    case DroneManager.DOME_FORMATION:
                        value -= Maths.GetPercentage(value, 50);
                        break;
                    case DroneManager.TURTLE_FORMATION:
                        value -= Maths.GetPercentage(value, (int)7.5);
                        break;
                    case DroneManager.ARROW_FORMATION:
                        value -= Maths.GetPercentage(value, 3);
                        break;
                    case DroneManager.PINCER_FORMATION:
                        value += Maths.GetPercentage(value, 3);
                        break;
                    case DroneManager.HEART_FORMATION:
                        value -= Maths.GetPercentage(value, 5);
                        break;
                    case DroneManager.RING_FORMATION:
                        value -= Maths.GetPercentage(value, 25);
                        break;
                    case DroneManager.DRILL_FORMATION:
                        value += Maths.GetPercentage(value, 20);
                        break;
                    case DroneManager.WHEEL_FORMATION:
                        value -= Maths.GetPercentage(value, 20);
                        break;
                }

                value = Ship.GetLaserDamageBoost(value, FactionId, (Selected != null ? Selected.FactionId : 0));

                value += Storage.DamageBoost;

                return value;
            }
        }

        public int GetHonorBoost(int honor)
        {
            switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
            {
                case DroneManager.PINCER_FORMATION:
                    return honor += Maths.GetPercentage(honor, 5);
                default:
                    return honor;
            }
        }

        public override int RocketDamage
        {
            get
            {
                var value = AttackManager.GetRocketDamage();
                value += Maths.GetPercentage(value, GetShotOreUpgradePercentage(RefinementTypeModule.ROCKET));
                value += Maths.GetPercentage(value, GetSkillPercentage("Rocket Fusion"));

                switch (SettingsManager.Player.Settings.InGameSettings.selectedFormation)
                {
                    case DroneManager.TURTLE_FORMATION:
                        value -= Maths.GetPercentage(value, (int)7.5);
                        break;
                    case DroneManager.ARROW_FORMATION:
                        value += Maths.GetPercentage(value, 20);
                        break;
                    case DroneManager.STAR_FORMATION:
                        value += Maths.GetPercentage(value, 25);
                        break;
                    case DroneManager.DOUBLE_ARROW_FORMATION:
                        value += Maths.GetPercentage(value, 30);
                        break;
                    case DroneManager.CHEVRON_FORMATION:
                        value += Maths.GetPercentage(value, 50);
                        break;
                }
                return value;
            }
        }

        public double RocketMissProbability
        {
            get
            {
                var value = 0.1;
                value -= Maths.GetDoublePercentage(value, GetSkillPercentage("Heat-seeking Missiles"));

                if (Storage.PrecisionTargeter)
                    value = 0;

                return value;
            }
        }

        public double LaserMissProbabilityAgainst(Attackable target)
        {
            var missProbability = Storage.underPLD8 ? 0.5 : 0.1;
            missProbability -= Maths.GetDoublePercentage(missProbability, GetSkillPercentage("Electro-optics"));

            if (target is Player player)
                missProbability += Maths.GetDoublePercentage(missProbability, player.GetSkillPercentage("Evasive Maneuvers"));

            var aimReductionPercent = CpuManager?.GetAimCpuMissReductionPercent() ?? 0;
            if (aimReductionPercent > 0 && Xenomit >= 10)
                missProbability -= Maths.GetDoublePercentage(missProbability, aimReductionPercent);

            if (missProbability < 0)
                missProbability = 0;
            else if (missProbability > 1)
                missProbability = 1;

            return missProbability;
        }

        public bool UpdateActivatable(Activatable pEntity, bool pInRange)
        {
            if (Storage.InRangeAssets.ContainsKey(pEntity.Id))
            {
                if (!pInRange)
                {
                    if (pEntity is Portal portal && portal.Working)
                    {
                        if (CurrentInRangePortalId == pEntity.Id)
                        {
                            var nearest = Storage.InRangeAssets.Values
                                .OfType<Portal>()
                                .Where(x => x.Working && x.Id != pEntity.Id)
                                .OrderBy(x => Position.DistanceTo(x.Position))
                                .FirstOrDefault();

                            CurrentInRangePortalId = nearest?.Id ?? -1;
                        }
                    }
                    Storage.InRangeAssets.TryRemove(pEntity.Id, out pEntity);
                    return true;
                }
            }
            else
            {
                if (pInRange)
                {
                    if (pEntity is Portal portal && portal.Working)
                    {
                        if (CurrentInRangePortalId <= 0)
                        {
                            CurrentInRangePortalId = pEntity.Id;
                        }
                        else
                        {
                            var currentPortal = Spacemap.GetActivatableMapEntity(CurrentInRangePortalId) as Portal;
                            if (currentPortal == null || !currentPortal.Working || Position.DistanceTo(portal.Position) < Position.DistanceTo(currentPortal.Position))
                                CurrentInRangePortalId = pEntity.Id;
                        }
                    }
                    Storage.InRangeAssets.TryAdd(pEntity.Id, pEntity);
                    return true;
                }
            }
            return false;
        }

        public DateTime ConfigCooldown = new DateTime();
        public void ChangeConfiguration(string LootID)
        {
            if (ConfigCooldown.AddSeconds(5) < DateTime.Now || Storage.GodMode)
            {
                SendPacket("0|S|CFG|" + LootID);
                SetCurrentConfiguration(Convert.ToInt32(LootID));
                ConfigCooldown = DateTime.Now;
                //achievement();
            }
            else
            {
                SendPacket("0|A|STM|config_change_failed_time");
            }
        }

        public void SetCurrentConfiguration(int pCurrentConfiguration)
        {
            if (pCurrentConfiguration != 1 && pCurrentConfiguration != 2)
                return;

            CurrentConfig = Convert.ToInt32(pCurrentConfiguration);
            Settings.InGameSettings.currentConfig = CurrentConfig;

            QueryManager.SetEquipment(this);
            AttackManager.RocketLauncher.CurrentLoad = 0;
            AttackManager.RocketLauncher.ReloadingActive = false;
            AttackManager.RocketLauncher.LastReloadTime = DateTime.Now;
            SettingsManager.SetCurrentItems();
            SettingsManager.SendNewItemStatus(CpuManager.ROCKET_LAUNCHER);
            SettingsManager.SendSlotBarCommand();
            AttackManager.RocketLauncher.SendStatus();

            DroneManager.UpdateDrones();
            UpdateStatus();
        }

        public void SetTitle(string title, bool permanent = false)
        {
            Title = title;
            var packet = Title != "" ? $"0|n|t|{Id}|1|{Title}" : $"0|n|trm|{Id}";
            SendPacket(packet);
            SendPacketToInRangePlayers(packet);

            if (permanent)
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                    mySqlClient.ExecuteNonQuery($"UPDATE player_accounts SET title = '{Title}' WHERE userId = {Id}");
        }

        public byte[] GetBeaconCommand()
        {
            return BeaconCommand.write(1, 1, 1, 1, Storage.IsInDemilitarizedZone, Storage.RepairBotActivated, (SkillTree.engineering == 5),
                         "equipment_extra_repbot_rep-3", Storage.IsInRadiationZone);
        }

        public byte[] GetShipCreateCommand(Player otherPlayer, short relationType)
        {
            return ShipCreateCommand.write(
                Id,
                Ship.LootId,
                EquipExpansion,
                !EventManager.JackpotBattle.InEvent(this) ? Clan.Tag : "",
                !EventManager.JackpotBattle.InEvent(this) ? (otherPlayer.RankId == 21 ? $"{Name} - {Id}" : Name) : EventManager.JackpotBattle.Name,
                Position.X,
                Position.Y,
                FactionId,
                !EventManager.JackpotBattle.InEvent(this) ? Clan.Id : 0,
                RankId,
                false,
                new ClanRelationModule(!EventManager.JackpotBattle.InEvent(this) ? relationType : ClanRelationModule.NONE),
                GetRingsCount(),
                false,
                false,
                Invisible,
                !EventManager.JackpotBattle.InEvent(this) ? relationType : ClanRelationModule.NONE,
                !EventManager.JackpotBattle.InEvent(this) ? relationType : ClanRelationModule.NONE,
                VisualModifiers.Values.ToList(),
                new class_11d(class_11d.DEFAULT));
        }

        public byte[] GetShipInitializationCommand()
        {
            var clientMapId = Spacemap != null ? Spacemap.VisualMapId : 0;
            return ShipInitializationCommand.write(
                Id,
                Name,
                Ship.LootId,
                Speed,
                CurrentShieldPoints,
                MaxShieldPoints,
                CurrentHitPoints,
                MaxHitPoints,
                CargoInUse,
                CargoCapacity,
                CurrentNanoHull,
                MaxNanoHull,
                Position.X,
                Position.Y,
                clientMapId,
                FactionId,
                Clan.Id,
                EquipExpansion,
                Premium,
                Data.experience,
                Data.honor,
                (short)Level,
                Data.credits,
                Data.uridium,
                (float)(Data.jackpot / 100.0),
                RankId,
                Clan.Tag,
                GetRingsCount(),
                true,
                Invisible,
                true,
                VisualModifiers.Values.ToList());
        }

        public int GetRingsCount()
        {
            GetLeonovEffect(Spacemap.Id, FactionId);
            getShieldSkill();
            return WarRank == 1 ? 100 : WarRank == 2 ? 63 : WarRank == 3 ? 31 : WarRank == 4 ? 15 : WarRank == 5 ? 7 : WarRank == 6 ? 3 : WarRank == 7 ? 1 : 0;
        }

        public bool Attackable()
        {
            return (AttackManager.IshCooldown.AddMilliseconds(TimeManager.ISH_DURATION) > DateTime.Now || Invincible || Storage.GodMode) ? false : true;
        }

        public void SendCooldown(string itemId, int time, bool countdown = false)
        {
            SendCommand(UpdateMenuItemCooldownGroupTimerCommand.write(
            SettingsManager.GetCooldownType(itemId),
            new ClientUISlotBarCategoryItemTimerStateModule( countdown ? ClientUISlotBarCategoryItemTimerStateModule.ACTIVE : ClientUISlotBarCategoryItemTimerStateModule.short_2168), time, time));
        }

        public void UpdateCurrentCooldowns()
        {
            Settings.Cooldowns[AmmunitionManager.SMB_01] = AttackManager.SmbCooldown.ToString("yyyy-MM-dd HH:mm:ss");
            Settings.Cooldowns[AmmunitionManager.ISH_01] = AttackManager.IshCooldown.ToString("yyyy-MM-dd HH:mm:ss");
            Settings.Cooldowns[AmmunitionManager.EMP_01] = AttackManager.EmpCooldown.ToString("yyyy-MM-dd HH:mm:ss");
            Settings.Cooldowns["ammunition_mine"] = AttackManager.mineCooldown.ToString("yyyy-MM-dd HH:mm:ss");
            Settings.Cooldowns[AmmunitionManager.DCR_250] = AttackManager.dcr_250Cooldown.ToString("yyyy-MM-dd HH:mm:ss");
            Settings.Cooldowns[AmmunitionManager.PLD_8] = AttackManager.pld8Cooldown.ToString("yyyy-MM-dd HH:mm:ss");
            Settings.Cooldowns[AmmunitionManager.R_IC3] = AttackManager.r_ic3Cooldown.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var skill in Storage.Skills.Values)
                Settings.Cooldowns[skill.LootId] = Storage.Skills[skill.LootId].cooldown.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public void SetCurrentCooldowns()
        {
            if (Settings.Cooldowns[AmmunitionManager.SMB_01] != "")
            {
                var seconds = (int)(DateTime.Now.Subtract(DateTime.Parse(Settings.Cooldowns[AmmunitionManager.SMB_01]))).TotalSeconds;
                AttackManager.SmbCooldown = DateTime.Now.AddSeconds(-seconds);
            }

            if (Settings.Cooldowns[AmmunitionManager.ISH_01] != "")
            {
                var seconds = (int)(DateTime.Now.Subtract(DateTime.Parse(Settings.Cooldowns[AmmunitionManager.ISH_01]))).TotalSeconds;
                AttackManager.IshCooldown = DateTime.Now.AddSeconds(-seconds);
            }

            if (Settings.Cooldowns[AmmunitionManager.EMP_01] != "")
            {
                var seconds = (int)(DateTime.Now.Subtract(DateTime.Parse(Settings.Cooldowns[AmmunitionManager.EMP_01]))).TotalSeconds;
                AttackManager.EmpCooldown = DateTime.Now.AddSeconds(-seconds);
            }

            if (Settings.Cooldowns["ammunition_mine"] != "")
            {
                var seconds = (int)(DateTime.Now.Subtract(DateTime.Parse(Settings.Cooldowns["ammunition_mine"]))).TotalSeconds;
                AttackManager.mineCooldown = DateTime.Now.AddSeconds(-seconds);
            }

            if (Settings.Cooldowns[AmmunitionManager.DCR_250] != "")
            {
                var seconds = (int)(DateTime.Now.Subtract(DateTime.Parse(Settings.Cooldowns[AmmunitionManager.DCR_250]))).TotalSeconds;
                AttackManager.dcr_250Cooldown = DateTime.Now.AddSeconds(-seconds);
            }

            if (Settings.Cooldowns[AmmunitionManager.PLD_8] != "")
            {
                var seconds = (int)(DateTime.Now.Subtract(DateTime.Parse(Settings.Cooldowns[AmmunitionManager.PLD_8]))).TotalSeconds;
                AttackManager.pld8Cooldown = DateTime.Now.AddSeconds(-seconds);
            }

            if (Settings.Cooldowns[AmmunitionManager.R_IC3] != "")
            {
                var seconds = (int)(DateTime.Now.Subtract(DateTime.Parse(Settings.Cooldowns[AmmunitionManager.R_IC3]))).TotalSeconds;
                AttackManager.r_ic3Cooldown = DateTime.Now.AddSeconds(-seconds);
            }

            foreach (var skill in Storage.Skills.Values)
            {
                if (Settings.Cooldowns.ContainsKey(skill.LootId))
                {
                    var seconds = (int)(DateTime.Now.Subtract(DateTime.Parse(Settings.Cooldowns[skill.LootId]))).TotalSeconds;
                    skill.cooldown = DateTime.Now.AddSeconds(-seconds);
                }
            }
        }

        public void SelectEntity(int entityId)
        {
            if (AttackManager.Attacking)
                DisableAttack(SettingsManager.Player.Settings.InGameSettings.selectedLaser);

            try
            {
                if (InRangeCharacters.ContainsKey(entityId))
                {
                    var character = InRangeCharacters.Values.Where(x => x.Id == entityId).FirstOrDefault();

                    if (character != null && !character.Destroyed)
                    {
                        if (character is Player player && (player.AttackManager.EmpCooldown.AddMilliseconds(TimeManager.EMP_DURATION) > DateTime.Now)) return;
                        Selected = character;

                        SendCommand(ShipSelectionCommand.write(
                            character.Id,
                            character.Ship.Id,
                            character.CurrentShieldPoints,
                            character.MaxShieldPoints,
                            character.CurrentHitPoints,
                            character.MaxHitPoints,
                            character.CurrentNanoHull,
                            character.MaxNanoHull,
                            character.shieldeng ? true : false));
                    }
                }
                else if (Storage.InRangeAssets.ContainsKey(entityId))
                {
                    var asset = Storage.InRangeAssets.Values.Where(x => x.Id == entityId).FirstOrDefault();

                    if (asset != null && (asset is BattleStation || asset is Satellite || asset is GroupMapRelayStation) && !asset.Destroyed)
                    {
                        Selected = asset;

                        SendCommand(AssetInfoCommand.write(
                            asset.Id,
                            asset.GetAssetType(),
                            asset.GetVisualDesignId(),
                            asset.GetVisualExpansionStage(),
                            asset.CurrentHitPoints,
                            asset.MaxHitPoints,
                            asset.MaxShieldPoints > 0 ? true : false,
                            asset.CurrentShieldPoints,
                            asset.MaxShieldPoints
                            ));
                    }
                }
                else
                {
                    var relay = Spacemap?.GetActivatableMapEntity(entityId) as GroupMapRelayStation;
                    if (relay != null && relay.VisibleOnMap && !relay.Destroyed)
                    {
                        Selected = relay;

                        SendCommand(AssetInfoCommand.write(
                            relay.Id,
                            relay.GetAssetType(),
                            relay.GetVisualDesignId(),
                            relay.GetVisualExpansionStage(),
                            relay.CurrentHitPoints,
                            relay.MaxHitPoints,
                            relay.MaxShieldPoints > 0 ? true : false,
                            relay.CurrentShieldPoints,
                            relay.MaxShieldPoints
                            ));
                    }
                }

                if (Selected != null)
                {
                    Group?.UpdateTarget(this, new List<command_i3O> { new GroupPlayerTargetModule(new GroupPlayerShipModule(Selected is Player player ? player.Ship.GroupShipId : GroupPlayerShipModule.WRECK), Selected.Name, new GroupPlayerInformationsModule(Selected.CurrentHitPoints, Selected.MaxHitPoints, Selected.CurrentShieldPoints, Selected.MaxShieldPoints, Selected.CurrentNanoHull, Selected.MaxNanoHull)) });
                }
            }
            catch (Exception e)
            {
                Out.WriteLine("SelectEntity void exception " + e, "Player.cs");
                Logger.Log("error_log", $"- [Player.cs] SelectEntity void exception: {e}");
            }
        }

        public void ChangeShip(int shipId)
        {
            SkillManager.DisableAllSkills();
            Ship = GameManager.GetShip(shipId);
            EquipExpansion = PlayerShipExtension.GetDefaultLevel(Ship);
            QueryManager.SetEquipment(this);
            SkillManager.InitiateSkills(true);

            LastCombatTime = DateTime.Now.AddSeconds(-999);
            Spacemap.RemoveCharacter(this);
            CurrentInRangePortalId = -1;
            Deselection();
            Storage.InRangeAssets.Clear();
            InRangeCharacters.Clear();

            Spacemap.AddAndInitPlayer(this);
            UpdateStatus();
        }

        public async void Jump(int mapId, Position targetPosition)
        {
            Storage.Skills.TryGetValue(SkillManager.SPEARHEAD_ULTIMATE_CLOAK, out var ultimateCloakSkill);
            if (ultimateCloakSkill != null && ultimateCloakSkill.Active)
                ultimateCloakSkill.Disable();

            Storage.Jumping = true;
            await Task.Delay(Portal.JUMP_DELAY);

            LastCombatTime = DateTime.Now.AddSeconds(-999);
            Spacemap.RemoveCharacter(this);
            CurrentInRangePortalId = -1;
            Deselection();
            Storage.InRangeAssets.Clear();
            InRangeCharacters.Clear();
            SetPosition(targetPosition);

            Spacemap = GameManager.GetSpacemap(mapId);

            Spacemap.AddAndInitPlayer(this);
            BoosterManager.Update();
            Storage.Jumping = false;
        }

        public void KillScreen(Attackable killerEntity, DestructionType destructionType, bool killedLogin = false)
        {
            var killScreenOptionModules = new List<KillScreenOptionModule>();
            var basicRepair =
                   new KillScreenOptionModule(new KillScreenOptionTypeModule(KillScreenOptionTypeModule.BASIC_REPAIR),
                                              new PriceModule(PriceModule.URIDIUM, 0), true, 0,
                                              new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                              new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                              new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                              new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()));

            var portalRepairTime = (int)(15 - ((DateTime.Now - Storage.KillscreenPortalRepairTime).TotalSeconds));
            var portalRepairPrice = 200;
            var portalRepair =
                  new KillScreenOptionModule(new KillScreenOptionTypeModule(KillScreenOptionTypeModule.AT_JUMPGATE_REPAIR),
                                             new PriceModule(PriceModule.URIDIUM, portalRepairPrice), Data.uridium >= portalRepairPrice, portalRepairTime,
                                             new MessageLocalizedWildcardCommand("desc_killscreen_repair_gate", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule> { new MessageWildcardReplacementModule("%COUNT%", portalRepairPrice.ToString(), new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED)) }),
                                             new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                             new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                             new MessageLocalizedWildcardCommand(Data.uridium >= portalRepairPrice ? "btn_killscreen_repair_for_uri" : "btn_killscreen_payment", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule> { new MessageWildcardReplacementModule("%COUNT%", portalRepairPrice.ToString(), new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED)) }));

            var deathLocationRepairTime = (int)(30 - ((DateTime.Now - Storage.KillscreenDeathLocationRepairTime).TotalSeconds));
            var deathLocationRepairPrice = 300;
            var deathLocationRepair =
                  new KillScreenOptionModule(new KillScreenOptionTypeModule(KillScreenOptionTypeModule.AT_DEATHLOCATION_REPAIR),
                                             new PriceModule(PriceModule.URIDIUM, deathLocationRepairPrice), Data.uridium >= deathLocationRepairPrice, deathLocationRepairTime,
                                             new MessageLocalizedWildcardCommand("desc_killscreen_repair_location", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule> { new MessageWildcardReplacementModule("%COUNT%", deathLocationRepairPrice.ToString(), new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED)) }),
                                             new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                             new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                             new MessageLocalizedWildcardCommand(Data.uridium >= deathLocationRepairPrice ? "btn_killscreen_repair_for_uri" : "btn_killscreen_payment", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule> { new MessageWildcardReplacementModule("%COUNT%", deathLocationRepairPrice.ToString(), new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED)) }));

            var fullRepair =
                   new KillScreenOptionModule(new KillScreenOptionTypeModule(KillScreenOptionTypeModule.BASIC_FULL_REPAIR),
                                              new PriceModule(PriceModule.URIDIUM, 0), true, 0,
                                              new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                              new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                              new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()),
                                              new MessageLocalizedWildcardCommand("btn_killscreen_repair_for_free", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule>()));
            killScreenOptionModules.Add(basicRepair);

            if (!killedLogin)
            {
                if (Spacemap.Activatables.FirstOrDefault(x => x.Value is Portal).Value is Portal portal && portal.Working && Data.uridium >= portalRepairPrice)
                    killScreenOptionModules.Add(portalRepair);

                if (Spacemap.Options.DeathLocationRepair && Data.uridium >= deathLocationRepairPrice)
                    killScreenOptionModules.Add(deathLocationRepair);

                //killScreenOptionModules.Add(fullRepair);
            }

            var killScreenPostCommand =
                    KillScreenPostCommand.write(killerEntity != null ? killerEntity.Name : "", "http://localhost/indexInternal.es?action=internalDock",
                                              "MISC", new DestructionTypeModule((short)destructionType),
                                              killScreenOptionModules);

            SendCommand(killScreenPostCommand);
        }

        public void Respawn(bool basicRepair = false, bool deathLocation = false, bool atNearestPortal = false, bool fullRepair = false)
        {
            LastCombatTime = DateTime.Now.AddSeconds(-999);

            AddVisualModifier(VisualModifierCommand.INVINCIBILITY, 0, "", 0, true);

            Storage.IsInDemilitarizedZone = basicRepair || fullRepair ? true : false;
            Storage.IsInEquipZone = basicRepair || fullRepair ? true : false;
            Storage.IsInRadiationZone = false;

            if (atNearestPortal)
                SetPosition(GetNearestPortalPosition());
            else if (deathLocation)
                CurrentHitPoints = Maths.GetPercentage(MaxHitPoints, 10);
            else
            {
                CurrentHitPoints = Maths.GetPercentage(MaxHitPoints, 1);
                SetPosition(GetBasePosition());
            }

            if (basicRepair || fullRepair)
                Spacemap = GameManager.GetSpacemap(GetBaseMapId());

            if (fullRepair)
            {
                CurrentHitPoints = MaxHitPoints;
                CurrentShieldConfig1 = MaxShieldPoints;
                CurrentShieldConfig2 = MaxShieldPoints;
            }

            Spacemap.AddAndInitPlayer(this, Destroyed);

            Group?.UpdateTarget(this, new List<command_i3O> { new GroupPlayerDisconnectedModule(false) });

            Destroyed = false;
        }

        public int GetBaseMapId()
        {
            int basemap = FactionId == 1 ? 1 : FactionId == 2 ? 5 : 9;
            if (Spacemap != null && (Spacemap.Id >= 16 && Spacemap.Id <= 29) && Level >= 12)
                basemap = FactionId == 1 ? 20 : FactionId == 2 ? 24 : 28;
            return basemap;
        }

        public Position GetBasePosition()
        {
            var baseposition = FactionId == 1 ? Position.MMOPosition : FactionId == 2 ? Position.EICPosition : Position.VRUPosition;
            if ((Spacemap.Id >= 16 && Spacemap.Id <= 29) && Level >= 12)
                baseposition = FactionId == 1 ? Position.NewMMOPosition : FactionId == 2 ? Position.NewEICPosition : Position.NewVRUPosition;

            return baseposition;
        }

        public void ChangeData(DataType dataType, int amount, ChangeType changeType = ChangeType.INCREASE)
        {
            if (amount == 0) return;
            amount = Convert.ToInt32(amount);
            var currentTickId = TickManager.CurrentTickId;
            var signedAmount = changeType == ChangeType.DECREASE ? -amount : amount;

            if (pendingDataTickId != -1 && currentTickId != pendingDataTickId && pendingDataChanges.Count > 0)
                FlushPendingDataChanges();

            pendingDataTickId = currentTickId;
            if (pendingDataChanges.ContainsKey(dataType))
                pendingDataChanges[dataType] += signedAmount;
            else
                pendingDataChanges.Add(dataType, signedAmount);

            switch (dataType)
            {
                case DataType.URIDIUM:
                    Data.uridium += signedAmount;
                    if (Data.uridium < 0) Data.uridium = 0;
                    break;
                case DataType.CREDITS:
                    Data.credits += signedAmount;
                    if (Data.credits < 0) Data.credits = 0;
                    break;
                case DataType.HONOR:
                    Data.honor += signedAmount;
                    if (Data.honor < 0) Data.honor = 0;
                    break;
                case DataType.EXPERIENCE:
                    Data.experience += signedAmount;
                    if (Data.experience < 0) Data.experience = 0;
                    pendingExperienceLevelCheck = true;
                    break;
                case DataType.JACKPOT:
                    Data.jackpot += signedAmount;
                    if (Data.jackpot < 0) Data.jackpot = 0;
                    break;
            }
        }

        private readonly Dictionary<DataType, int> pendingDataChanges = new Dictionary<DataType, int>();
        private long pendingDataTickId = -1;
        private bool pendingExperienceLevelCheck;

        private void FlushPendingDataChanges()
        {
            if (pendingDataChanges.Count == 0)
                return;

            foreach (var change in pendingDataChanges.ToList())
            {
                if (change.Value == 0)
                    continue;

                var amount = Math.Abs(change.Value);
                var prefix = change.Value < 0 ? "-" : "";

                switch (change.Key)
                {
                    case DataType.URIDIUM:
                        SendPacket($"0|LM|ST|URI|{prefix}{amount}|{Data.uridium}");
                        break;
                    case DataType.CREDITS:
                        SendPacket($"0|LM|ST|CRE|{prefix}{amount}|{Data.credits}");
                        break;
                    case DataType.HONOR:
                        SendPacket($"0|LM|ST|HON|{prefix}{amount}|{Data.honor}");
                        break;
                    case DataType.EXPERIENCE:
                        SendPacket($"0|LM|ST|EP|{prefix}{amount}|{Data.experience}|{Level}");
                        break;
                    case DataType.JACKPOT:
                        var jackpotDelta = (amount / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
                        var jackpotTotal = (Data.jackpot / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
                        SendPacket($"0|LM|ST|JPE|{prefix}{jackpotDelta}|{jackpotTotal}");
                        break;
                }
            }

            if (pendingExperienceLevelCheck)
            {
                CheckNextLevel(Data.experience);
                pendingExperienceLevelCheck = false;
            }

            QueryManager.SavePlayer.Information(this);
            pendingDataChanges.Clear();
            pendingDataTickId = -1;
        }

        private static readonly Dictionary<Ores, int> OreBasePrices = new Dictionary<Ores, int>
        {
            { Ores.Prometium, 10 },
            { Ores.Endurium, 15 },
            { Ores.Terbium, 25 },
            { Ores.Prometid, 200 },
            { Ores.Duranium, 200 },
            { Ores.Promerium, 500 }
        };

        private int GetOreAmount(Ores oreType)
        {
            switch (oreType)
            {
                case Ores.Prometium: return Prometium;
                case Ores.Endurium: return Endurium;
                case Ores.Terbium: return Terbium;
                case Ores.Prometid: return Prometid;
                case Ores.Duranium: return Duranium;
                case Ores.Promerium: return Promerium;
                case Ores.Xenomit: return Xenomit;
                case Ores.Seprom: return Seprom;
                case Ores.Palladium: return Palladium;
                default: return 0;
            }
        }

        private void SetOreAmount(Ores oreType, int amount)
        {
            amount = Math.Max(0, amount);
            switch (oreType)
            {
                case Ores.Prometium:
                    Prometium = amount;
                    break;
                case Ores.Endurium:
                    Endurium = amount;
                    break;
                case Ores.Terbium:
                    Terbium = amount;
                    break;
                case Ores.Prometid:
                    Prometid = amount;
                    break;
                case Ores.Duranium:
                    Duranium = amount;
                    break;
                case Ores.Promerium:
                    Promerium = amount;
                    break;
                case Ores.Xenomit:
                    Xenomit = amount;
                    break;
                case Ores.Seprom:
                    Seprom = amount;
                    break;
                case Ores.Palladium:
                    Palladium = amount;
                    break;
            }
        }

        public void SendCargoStatus()
        {
            SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.CARGO_CHANGE}|{CargoCapacity}|{1}");
        }

        public void SendCargoFullWarning()
        {
            SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.SERVER_MSG}|Cargo full ({CargoInUse}/{CargoCapacity})");
        }

        public void SendOreCount()
        {
            SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.SET_ORE_COUNT}|{Prometium}|{Endurium}|{Terbium}|{Prometid}|{Duranium}|{Promerium}|{Xenomit}|{Seprom}|{Palladium}");
        }

        private List<OreStackCommand> GetModernOreStacks()
        {
            return new List<OreStackCommand>
            {
                OreStackCommand.FromServerOre(Ores.Prometium, Prometium),
                OreStackCommand.FromServerOre(Ores.Endurium, Endurium),
                OreStackCommand.FromServerOre(Ores.Terbium, Terbium),
                OreStackCommand.FromServerOre(Ores.Prometid, Prometid),
                OreStackCommand.FromServerOre(Ores.Duranium, Duranium),
                OreStackCommand.FromServerOre(Ores.Promerium, Promerium),
                OreStackCommand.FromServerOre(Ores.Xenomit, Xenomit),
                OreStackCommand.FromServerOre(Ores.Seprom, Seprom),
                OreStackCommand.FromServerOre(Ores.Palladium, Palladium)
            };
        }

        private static Ores GetDefaultUpgradeOre(short refinementType)
        {
            switch (refinementType)
            {
                case RefinementTypeModule.LASER:
                    return Ores.Prometid;
                case RefinementTypeModule.ROCKET:
                    return Ores.Duranium;
                case RefinementTypeModule.DRIVING:
                    return Ores.Promerium;
                case RefinementTypeModule.SHIELD:
                    return Ores.Seprom;
                default:
                    return Ores.Prometid;
            }
        }

        private double GetUpgradeDisplayCount(short refinementType)
        {
            var upgrade = GetOreUpgrade(refinementType);
            if (upgrade == null || upgrade.resource < 0 ||
                GetOreUpgradePercentage(upgrade.resource, refinementType) <= 0)
                return 0;

            if (refinementType == RefinementTypeModule.LASER || refinementType == RefinementTypeModule.ROCKET)
                return Math.Ceiling(upgrade.amount / 10.0);

            var remainingMinutes = (upgrade.expiresAt - DateTime.UtcNow).TotalMinutes;
            return remainingMinutes > 0 ? Math.Ceiling(remainingMinutes) : 0;
        }

        private OreStackCommand GetModernUpgradeStack(short refinementType)
        {
            var upgrade = GetOreUpgrade(refinementType);
            var ore = upgrade != null && upgrade.resource >= 0 &&
                      GetOreUpgradePercentage(upgrade.resource, refinementType) > 0
                ? (Ores)upgrade.resource
                : GetDefaultUpgradeOre(refinementType);

            return OreStackCommand.FromServerOre(ore, GetUpgradeDisplayCount(refinementType));
        }

        public void SendModernOreState()
        {
            var stacks = GetModernOreStacks();
            SendCommand(OreCountUpdateCommand.write(stacks));
        }

        public void SendModernOreRefinementState()
        {
            if (!EnableModernOreRefinementSync)
                return;

            SendModernOreRefinementState(
                RefinementTypeModule.LASER,
                RefinementTypeModule.ROCKET,
                RefinementTypeModule.DRIVING,
                RefinementTypeModule.SHIELD);

            var now = DateTime.UtcNow;
            nextShotOreRefinementSyncAt = now.AddSeconds(ShotOreRefinementSyncIntervalSeconds);
            nextTimedOreRefinementSyncAt = now.AddSeconds(TimedOreRefinementSyncIntervalSeconds);
        }

        private bool HasActiveTimedOreUpgrades()
        {
            if (Data == null)
                return false;

            var now = DateTime.UtcNow;
            return (Data.drivingUpgrade != null && Data.drivingUpgrade.resource >= 0 && Data.drivingUpgrade.expiresAt > now) ||
                   (Data.shieldUpgrade != null && Data.shieldUpgrade.resource >= 0 && Data.shieldUpgrade.expiresAt > now);
        }

        private void SendModernOreRefinementState(params short[] refinementTypes)
        {
            if (!EnableModernOreRefinementSync || refinementTypes == null || refinementTypes.Length == 0)
                return;

            var refinementEntries = new List<OreRefinementEntryCommand>();
            foreach (var refinementType in refinementTypes)
            {
                refinementEntries.Add(new OreRefinementEntryCommand(
                    new RefinementTypeModule(refinementType),
                    GetModernUpgradeStack(refinementType)));
            }

            SendCommand(OreRefinementUpdateCommand.write(refinementEntries));
        }

        public double GetHonorOreFactor()
        {
            var factor = 1 + (Data.honor / 500000.0);
            if (factor > 2) factor = 2;
            if (factor < 0) factor = 0;
            return factor;
        }

        public int GetOreSellPrice(Ores oreType)
        {
            if (!OreBasePrices.TryGetValue(oreType, out var basePrice))
                return 0;

            return (int)Math.Round(basePrice * GetHonorOreFactor(), MidpointRounding.AwayFromZero);
        }

        public int ChangeCargo(Ores oreType, int amount, bool notify = true, bool persist = true, bool sync = true)
        {
            if (amount == 0) return 0;

            var current = GetOreAmount(oreType);
            var applied = amount;

            if (amount > 0)
            {
                var space = FreeCargo;
                if (space <= 0)
                {
                    if (notify) SendCargoFullWarning();
                    return 0;
                }

                if (amount > space)
                {
                    applied = space;
                    if (notify) SendCargoFullWarning();
                }
            }
            else
            {
                var removable = Math.Min(current, -amount);
                if (removable <= 0)
                    return 0;
                applied = -removable;
            }

            SetOreAmount(oreType, current + applied);

            if (notify && applied > 0)
            {
                var resourceKey = GetOreResourceKey(oreType);
                if (!string.IsNullOrEmpty(resourceKey))
                    SendPacket($"0|{ServerCommands.BOX_COLLECT_RESPONSE}|{ServerCommands.BOX_CONTENT_ORE}|{resourceKey}|{applied}");
            }

            if (sync)
            {
                SendCargoStatus();
                SendOreCount();
                SendModernOreState();
            }

            if (persist)
            {
                QueryManager.SavePlayer.Information(this);
            }

            return applied;
        }

        private static string GetOreResourceKey(Ores oreType)
        {
            switch (oreType)
            {
                case Ores.Prometium: return "ore_prometium";
                case Ores.Endurium: return "ore_endurium";
                case Ores.Terbium: return "ore_terbium";
                case Ores.Xenomit: return "ore_xenomit";
                case Ores.Prometid: return "ore_prometid";
                case Ores.Duranium: return "ore_duranium";
                case Ores.Promerium: return "ore_promerium";
                case Ores.Seprom: return "ore_seprom";
                case Ores.Palladium: return "ore_palladium";
                default: return null;
            }
        }

        public bool TrySellOre(Ores oreType, int amount)
        {
            if (amount <= 0 || !OreBasePrices.ContainsKey(oreType))
                return false;

            var stock = GetOreAmount(oreType);
            if (stock <= 0) return false;

            var sellAmount = Math.Min(stock, amount);
            var unitPrice = GetOreSellPrice(oreType);
            if (unitPrice <= 0 || sellAmount <= 0) return false;

            ChangeCargo(oreType, -sellAmount, false, false, false);
            ChangeData(DataType.CREDITS, sellAmount * unitPrice);
            SendCargoStatus();
            SendOreCount();
            SendModernOreState();
            QueryManager.SavePlayer.Information(this);
            SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.SERVER_MSG}|Sold {sellAmount} {oreType} for {sellAmount * unitPrice} credits.");
            return true;
        }

        public bool TryUpgrade(short refinementType, Ores oreType, int amount)
        {
            if (amount <= 0 || !IsUpgradeResourceAllowed(refinementType, oreType))
                return false;

            if (GetOreAmount(oreType) < amount)
                return false;

            var upgrade = GetOreUpgrade(refinementType);
            if (upgrade == null)
                return false;

            var now = DateTime.UtcNow;
            if (refinementType == RefinementTypeModule.LASER || refinementType == RefinementTypeModule.ROCKET)
            {
                var boostedShots = (long)amount * 10L;
                var existingShots = upgrade.resource == (int)oreType ? Math.Max(0, upgrade.amount) : 0;
                if (boostedShots > int.MaxValue || (long)existingShots + boostedShots > int.MaxValue)
                    return false;

                upgrade.amount = (int)(existingShots + boostedShots);
                upgrade.resource = (int)oreType;
                upgrade.expiresAt = DateTime.MinValue;
            }
            else
            {
                var baseTime = upgrade.resource == (int)oreType && upgrade.expiresAt > now
                    ? upgrade.expiresAt
                    : now;
                DateTime expiresAt;
                try
                {
                    expiresAt = baseTime.AddMinutes((double)amount * 10.0);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }

                upgrade.resource = (int)oreType;
                upgrade.amount = 0;
                upgrade.expiresAt = expiresAt;
            }

            ChangeCargo(oreType, -amount, false, false, false);
            SendCargoStatus();
            SendOreCount();
            SendModernOreState();
            UpdateStatus();
            QueryManager.SavePlayer.Information(this);
            SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.SERVER_MSG}|Upgrade complete: {amount} {oreType} applied to {GetUpgradeName(refinementType)}.");
            return true;
        }

        public bool TryRefine(Ores targetOre, int amount = 1)
        {
            if (amount <= 0) amount = 1;

            int batches;
            switch (targetOre)
            {
                case Ores.Prometid:
                    batches = amount;
                    var prometiumRequired = (long)batches * 20L;
                    var enduriumForPrometid = (long)batches * 10L;
                    if (prometiumRequired > int.MaxValue || enduriumForPrometid > int.MaxValue ||
                        Prometium < prometiumRequired || Endurium < enduriumForPrometid)
                        return false;
                    ChangeCargo(Ores.Prometium, -(int)prometiumRequired, false, false, false);
                    ChangeCargo(Ores.Endurium, -(int)enduriumForPrometid, false, false, false);
                    ChangeCargo(Ores.Prometid, batches, false, false, false);
                    break;
                case Ores.Duranium:
                    batches = amount;
                    var terbiumRequired = (long)batches * 20L;
                    var enduriumForDuranium = (long)batches * 10L;
                    if (terbiumRequired > int.MaxValue || enduriumForDuranium > int.MaxValue ||
                        Terbium < terbiumRequired || Endurium < enduriumForDuranium)
                        return false;
                    ChangeCargo(Ores.Terbium, -(int)terbiumRequired, false, false, false);
                    ChangeCargo(Ores.Endurium, -(int)enduriumForDuranium, false, false, false);
                    ChangeCargo(Ores.Duranium, batches, false, false, false);
                    break;
                case Ores.Promerium:
                    batches = amount;
                    var prometidRequired = (long)batches * 10L;
                    var duraniumRequired = (long)batches * 10L;
                    if (prometidRequired > int.MaxValue || duraniumRequired > int.MaxValue ||
                        Prometid < prometidRequired || Duranium < duraniumRequired || Xenomit < batches)
                        return false;
                    ChangeCargo(Ores.Prometid, -(int)prometidRequired, false, false, false);
                    ChangeCargo(Ores.Duranium, -(int)duraniumRequired, false, false, false);
                    ChangeCargo(Ores.Xenomit, -batches, false, false, false);
                    ChangeCargo(Ores.Promerium, batches, false, false, false);
                    break;
                default:
                    return false;
            }

            SendCargoStatus();
            SendOreCount();
            SendModernOreState();
            QueryManager.SavePlayer.Information(this);
            SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.SERVER_MSG}|Refinement complete: +{amount} {targetOre}.");
            return true;
        }

        public void SendOreShopInfo()
        {
            SendCargoStatus();
            SendOreCount();
            SendModernOreState();
            SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.SET_ORE_PRICES}|{GetOreSellPrice(Ores.Prometium)}|{GetOreSellPrice(Ores.Endurium)}|{GetOreSellPrice(Ores.Terbium)}|{GetOreSellPrice(Ores.Prometid)}|{GetOreSellPrice(Ores.Duranium)}|{GetOreSellPrice(Ores.Promerium)}");
        }

        public void CheckNextLevel(long experience)
        {
            short lvl = 1;
            long expNext = 10000;

            while (experience >= expNext)
            {
                expNext *= 2;
                lvl++;
            }

            if (lvl > Level)
            {
                SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.LEVEL_UPDATE}|{lvl}|{expNext - experience}");
                var levelUpCommand = LevelUpCommand.write(Id, lvl);
                SendCommand(levelUpCommand);
                SendCommandToInRangePlayers(levelUpCommand);
                Level = lvl;
                QueryManager.SavePlayer.Information(this);
            }
        }

        public void UpdateShipStatus()
        {
            ShipStatus.hp = CurrentHitPoints;
            ShipStatus.shd1 = CurrentShieldConfig1;
            ShipStatus.shd2 = CurrentShieldConfig2;
            ShipStatus.invis = Invisible == true ? 1 : 0;
        }

        public bool AttackingOrUnderAttack(int combatSecond = 10)
        {
            if (LastCombatTime.AddSeconds(combatSecond) > DateTime.Now) return true;
            if (LastAttackTime(combatSecond)) return true;
            return false;
        }

        public bool LastAttackTime(int combatSecond = 10)
        {
            if (AttackManager.lastAttackTime.AddSeconds(combatSecond) > DateTime.Now) return true;
            if (AttackManager.lastRSBAttackTime.AddSeconds(combatSecond) > DateTime.Now) return true;
            if (AttackManager.lastRocketAttack.AddSeconds(combatSecond) > DateTime.Now) return true;
            return false;
        }

        public void EnableAttack(string itemId)
        {
            AttackManager.Attacking = true;
            SendCommand(AddMenuItemHighlightCommand.write(new class_h2P(class_h2P.ITEMS_CONTROL), itemId, new class_K18(class_K18.ACTIVE), new class_I1W(false, 0)));
        }

        public void DisableAttack(string itemId)
        {
            AttackManager.Attacking = false;
            SendCommand(RemoveMenuItemHighlightCommand.write(new class_h2P(class_h2P.ITEMS_CONTROL), itemId, new class_K18(class_K18.ACTIVE)));
        }

        public Position GetNearestPortalPosition()
        {
            var activatablesOrdered = Spacemap.Activatables.Values.OrderBy(x => x.Position.DistanceTo(Position));
            var nearestPortal = activatablesOrdered.FirstOrDefault(x => x is Portal);

            return nearestPortal.Position;
        }

        public void SaveSettings()
        {
            QueryManager.SavePlayer.Settings(this, "audio", Settings.Audio);
            QueryManager.SavePlayer.Settings(this, "quality", Settings.Quality);
            QueryManager.SavePlayer.Settings(this, "classY2T", Settings.ClassY2T);
            QueryManager.SavePlayer.Settings(this, "display", Settings.Display);
            QueryManager.SavePlayer.Settings(this, "gameplay", Settings.Gameplay);
            QueryManager.SavePlayer.Settings(this, "window", Settings.Window);
            QueryManager.SavePlayer.Settings(this, "boundKeys", Settings.BoundKeys);
            QueryManager.SavePlayer.Settings(this, "inGameSettings", Settings.InGameSettings);
            QueryManager.SavePlayer.Settings(this, "cooldowns", Settings.Cooldowns);
            QueryManager.SavePlayer.Settings(this, "slotbarItems", Settings.SlotBarItems);
            QueryManager.SavePlayer.Settings(this, "premiumSlotbarItems", Settings.PremiumSlotBarItems);
            QueryManager.SavePlayer.Settings(this, "proActionBarItems", Settings.ProActionBarItems);
        }

        public void SendPacket(string packet)
        {
            try
            {
                var gameSession = GameManager.GetGameSession(Id);

                if (gameSession == null) return;
                if (!Program.TickManager.Exists(this)) return;
                if (gameSession.Client.Socket == null || !gameSession.Client.Socket.IsBound || !gameSession.Client.Socket.Connected) return;

                PacketDebug.NotifyLegacyOutgoing(this, packet);
                gameSession.Client.Send(LegacyModule.write(packet));
            }
            catch (Exception e)
            {
                Out.WriteLine("SendPacket void exception " + e, "Player.cs");
                Logger.Log("error_log", $"- [Player.cs] SendPacket void exception: {e}");
            }
        }

        public void SendCommand(byte[] command)
        {
            try
            {
                var gameSession = GameManager.GetGameSession(Id);

                if (gameSession == null) return;
                if (!Program.TickManager.Exists(this)) return;
                if (gameSession.Client.Socket == null || !gameSession.Client.Socket.IsBound || !gameSession.Client.Socket.Connected) return;

                PacketDebug.NotifyOutgoing(this, command);
                gameSession.Client.Send(command);
            }
            catch (Exception e)
            {
                Out.WriteLine("SendCommand void exception " + e, "Player.cs");
                Logger.Log("error_log", $"- [Player.cs] SendCommand void exception: {e}");
            }
        }

        public bool LoggingOut = false;
        private DateTime LogoutStartTime = new DateTime();

        public void Logout(bool start = false)
        {
            if (start)
            {
                LoggingOut = true;
                LogoutStartTime = DateTime.Now;
                return;
            }

            if (!LoggingOut) return;

            if (!Storage.IsInDemilitarizedZone && (AttackingOrUnderAttack() || Moving || Spacemap.Options.LogoutBlocked))
            {
                AbortLogout();
                return;
            }

            if (LogoutStartTime.AddSeconds((Premium || RankId == 21) ? 5 : 10) < DateTime.Now)
            {
                SendPacket("0|l|" + Id);
                GameSession.Disconnect(GameSession.DisconnectionType.NORMAL);
                LoggingOut = false;
            }

        }

        public void AbortLogout()
        {
            LoggingOut = false;
            SendPacket("0|t");
        }

        public void getShieldSkill()
        {
            if (SkillTree.shieldEngineering == 5)
                shieldeng = true;
        }

        private int GetScaledSkillValue(int level, params int[] levelValues)
        {
            if (level <= 0 || levelValues == null || levelValues.Length == 0)
                return 0;

            if (level > levelValues.Length)
                level = levelValues.Length;

            return levelValues[level - 1];
        }

        public int GetSkillPercentage(string skillName)
        {
            if (skillName == "Shield Engineering" || skillName == "Shield Engeneering")
                return GetScaledSkillValue(SkillTree.shieldEngineering, 4, 8, 12, 18, 25);
            if (skillName == "Engineering")
                return GetScaledSkillValue(SkillTree.engineering, 5, 10, 15, 20, 25);
            if (skillName == "Detonation" || skillName == "Detonation I" || skillName == "Detonation II")
                return SkillTree.detonation2 >= 1 ? GetScaledSkillValue(SkillTree.detonation2, 21, 28, 50) : GetScaledSkillValue(SkillTree.detonation1, 7, 14);
            if (skillName == "Heat-seeking Missiles" || skillName == "Heat-seeking missiles")
                return GetScaledSkillValue(SkillTree.heatseekingMissiles, 1, 2, 4, 6, 10);
            if (skillName == "Rocket Fusion")
                return GetScaledSkillValue(SkillTree.rocketFusion, 2, 4, 6, 8, 15);
            if (skillName == "Cruelty" || skillName == "Cruelty I" || skillName == "Cruelty II")
                return SkillTree.cruelty2 >= 1 ? GetScaledSkillValue(SkillTree.cruelty2, 12, 18, 25) : GetScaledSkillValue(SkillTree.cruelty1, 4, 8);
            if (skillName == "Explosives")
                return GetScaledSkillValue(SkillTree.explosives, 4, 8, 12, 18, 25);
            if (skillName == "Luck" || skillName == "Luck I" || skillName == "Luck II")
                return SkillTree.luck2 >= 1 ? GetScaledSkillValue(SkillTree.luck2, 6, 8, 12) : GetScaledSkillValue(SkillTree.luck1, 2, 4);
            if (skillName == "Bounty Hunter" || skillName == "Bounty Hunter I" || skillName == "Bounty Hunter II" || skillName == "Bouty Hunter I" || skillName == "Bouty Hunter II")
                return SkillTree.bountyhunter2 >= 1 ? GetScaledSkillValue(SkillTree.bountyhunter2, 6, 8, 12) : GetScaledSkillValue(SkillTree.bountyhunter1, 2, 4);
            if (skillName == "Shield Mechanics")
                return GetScaledSkillValue(SkillTree.shieldMechanics, 2, 4);
            if (skillName == "Electro-optics" || skillName == "Electro-Optics")
                return GetScaledSkillValue(SkillTree.electroOptics, 5, 10, 15, 20, 25);
            if (skillName == "Ship Hull" || skillName == "Ship Hull I" || skillName == "Ship Hull II")
                return SkillTree.shiphull2 >= 1 ? GetScaledSkillValue(SkillTree.shiphull2, 15000, 25000, 50000) : GetScaledSkillValue(SkillTree.shiphull1, 5000, 10000);
            if (skillName == "Tactics")
                return GetScaledSkillValue(SkillTree.tactics, 2, 4, 6, 8, 12);
            if (skillName == "Greed")
                return GetScaledSkillValue(SkillTree.greed, 4, 8, 12, 18, 25);
            if (skillName == "Alien Hunter")
                return GetScaledSkillValue(SkillTree.alienHunter, 2, 4, 6, 8, 12);
            if (skillName == "Tractor Beam I")
                return GetScaledSkillValue(SkillTree.tractorBeam1, 1, 2, 3, 4, 6);
            if (skillName == "Tractor Beam II")
                return GetScaledSkillValue(SkillTree.tractorBeam2, 2, 6, 10, 15, 20);
            if (skillName == "Evasive Maneuvers" || skillName == "Evasive maneuvers I" || skillName == "Evasive maneuvers II")
                return SkillTree.evasiveManeuvers2 >= 1 ? GetScaledSkillValue(SkillTree.evasiveManeuvers2, 6, 8, 12) : GetScaledSkillValue(SkillTree.evasiveManeuvers1, 2, 4);
            if (skillName == "Logistics")
                return GetScaledSkillValue(SkillTree.logistics, 4, 8, 12, 18, 25);

            return 0;
        }

        public Boolean GetBountyHunter()
        {
            if (SkillTree.bountyhunter2 == 3)
                {
                return true;
            }
            else
            {
                return false;
            }
        }

        public Boolean GetShieldMechanics()
        {
            if (SkillTree.shieldMechanics >= 2)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void UpdateScore(int score, Boolean increse)
        {
            if (EventManager.Scoremageddon.Active == true)
            {
                if (increse == true)
                {
                    Score += score;
                }
                else
                {
                    Score -= score;
                }
                if (Score <= 0)
                {
                    Score = 0;
                }
            }

            else return;
        }

        public Boolean GetLeonovEffect(int map, int faction)
        {
            if (Spacemap.FactionId == faction && Ship.Id == Ship.LEONOV && Spacemap.Id < 12)
            {
                AddVisualModifier(VisualModifierCommand.LEONOV_EFFECT, 0, "", 0, true);
                FriendlyMap = true;
                return true;
            }
            else
            {
                RemoveVisualModifier(VisualModifierCommand.LEONOV_EFFECT);
                FriendlyMap = false;
                return false;
            }
        }

        public void achievement()
        {
            Achievements.Set(1, true, 1, true);
        }

        public void CameraLockToHero()
        {
            SendCommand(CameraLockToHeroCommand.write());
        }

        public void CameraLockToShip(int shipUserId, double zoomFactor = 1, double tweenDurationInSeconds = 3)
        {
            if (shipUserId <= 0) return;

            SendCommand(CameraLockToShipCommand.write(
                shipUserId,
                (float)zoomFactor,
                (float)tweenDurationInSeconds));
        }

        public void CameraLockToCoordinates(int x, int y, double tweenDurationInSeconds = 3)
        {
            SendCommand(CameraLockToCoordinatesCommand.write(
                x,
                y,
                (float)tweenDurationInSeconds));
        }


        public GameSession GameSession
        {
            get
            {
                return GameManager.GetGameSession(Id);
            }
        }

        public void LoadAmmo()
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var querySet = mySqlClient.ExecuteQueryRow($"SELECT * FROM player_accounts WHERE userId = {Id}");
                if (querySet == null || querySet["ammo"] == null)
                {
                    Ammo = new Ammo();
                    return;
                }

                dynamic ammo = JsonConvert.DeserializeObject(querySet["ammo"].ToString());
                if (ammo == null)
                {
                    Ammo = new Ammo();
                    return;
                }

                Ammo.cbo100 = ammo["cbo100"];
                Ammo.job100 = ammo["job100"];
                Ammo.rb214 = ammo["rb214"];
                Ammo.rsb75 = ammo["rsb75"];
                Ammo.sab50 = ammo["sab50"];
                Ammo.pib = ammo["pib"];
                Ammo.ish = ammo["ish"];
                Ammo.emp = ammo["emp"];
                Ammo.smb = ammo["smb"];
                Ammo.ice = ammo["ice"];
                Ammo.dcr = ammo["dcr"];
                Ammo.wiz = ammo["wiz"];
                Ammo.pld = ammo["pld"];
                Ammo.slm = ammo["slm"];
                Ammo.ddm = ammo["ddm"];
                Ammo.empm = ammo["empm"];
                Ammo.sabm = ammo["sabm"];
                Ammo.hstrm01 = ammo["hstrm01"];
                Ammo.ubr100 = ammo["ubr100"];
                Ammo.eco10 = ammo["eco10"];
                Ammo.sar01 = ammo["sar01"];
                Ammo.sar02 = ammo["sar02"];
                Ammo.lcb10 = ammo["lcb10"];
                Ammo.mcb25 = ammo["mcb25"];
                Ammo.mcb50 = ammo["mcb50"];
                Ammo.ucb100 = ammo["ucb100"];
                Ammo.r310 = ammo["r310"];
                Ammo.plt26 = ammo["plt26"];
                Ammo.plt21 = ammo["plt21"];
                Ammo.plt3030 = ammo["plt3030"];
            }
        }
        public int GetAmmoCount(string ammoId)
        {
            switch (ammoId)
            {
                case AmmunitionManager.LCB_10:
                    return Ammo.lcb10;
                case AmmunitionManager.MCB_25:
                    return Ammo.mcb25;
                case AmmunitionManager.MCB_50:
                    return Ammo.mcb50;
                case AmmunitionManager.UCB_100:
                    return Ammo.ucb100;
                case AmmunitionManager.SAB_50:
                    return Ammo.sab50;
                case AmmunitionManager.RSB_75:
                    return Ammo.rsb75;
                case AmmunitionManager.ISH_01:
                    return Ammo.ish;
                case AmmunitionManager.EMP_01:
                    return Ammo.emp;
                case AmmunitionManager.SMB_01:
                    return Ammo.smb;
                case AmmunitionManager.R_IC3:
                    return Ammo.ice;
                case AmmunitionManager.DCR_250:
                    return Ammo.dcr;
                case AmmunitionManager.WIZ_X:
                    return Ammo.wiz;
                case AmmunitionManager.PLD_8:
                    return Ammo.pld;
                case AmmunitionManager.CBO_100:
                    return Ammo.cbo100;
                case AmmunitionManager.JOB_100:
                    return Ammo.job100;
                case AmmunitionManager.RB_214:
                    return Ammo.rb214;
                case AmmunitionManager.CLK_XL:
                    return CpuManager != null ? CpuManager.GetCpuCount(CpuManager.CLOAK_XL_CPU, Ammo.cloacks) : Ammo.cloacks;
                case CpuManager.AUTO_ROCKET_CPU:
                    return CpuManager != null ? CpuManager.GetCpuCount(CpuManager.AUTO_ROCKET_CPU) : 0;
                case CpuManager.AUTO_HELLSTROM_CPU:
                    return CpuManager != null ? CpuManager.GetCpuCount(CpuManager.AUTO_HELLSTROM_CPU) : 0;
                case AmmunitionManager.R_310:
                    return Ammo.r310;
                case AmmunitionManager.PLT_2026:
                    return Ammo.plt26;
                case AmmunitionManager.PLT_2021:
                    return Ammo.plt21;
                case AmmunitionManager.PLT_3030:
                    return Ammo.plt3030;
                case AmmunitionManager.ROCKET_LAUNCHER_HSTRM_01:
                    return Ammo.hstrm01;
                case AmmunitionManager.ROCKET_LAUNCHER_SAR_02:
                    return Ammo.sar02;
                case AmmunitionManager.ROCKET_LAUNCHER_UBR_100:
                    return Ammo.ubr100;
                case AmmunitionManager.ROCKET_LAUNCHER_SAR_01:
                    return Ammo.sar01;
                case AmmunitionManager.ROCKET_LAUNCHER_ECO_10:
                    return Ammo.eco10;
                default:
                    if (CpuManager != null && !string.IsNullOrEmpty(ammoId) &&
                        (ammoId.StartsWith("equipment_extra_cpu_", StringComparison.OrdinalIgnoreCase) ||
                         ammoId.StartsWith("equipment_extra_repbot_", StringComparison.OrdinalIgnoreCase)))
                        return CpuManager.GetCpuCount(ammoId);

                    return 0;

            }
        }

        public void SubAmmo(string ammoId, int amount)
        {
            switch (ammoId)
            {
                case AmmunitionManager.LCB_10:
                    Ammo.lcb10 -= amount;
                    break;
                case AmmunitionManager.MCB_25:
                    Ammo.mcb25 -= amount;
                    break;
                case AmmunitionManager.MCB_50:
                    Ammo.mcb50 -= amount;
                    break;
                case AmmunitionManager.UCB_100:
                    Ammo.ucb100 -= amount;
                    break;
                case AmmunitionManager.SAB_50:
                    Ammo.sab50 -= amount;
                    break;
                case AmmunitionManager.RSB_75:
                    Ammo.rsb75 -= amount;
                    break;
                case AmmunitionManager.ISH_01:
                    Ammo.ish -= amount;
                    break;
                case AmmunitionManager.EMP_01:
                    Ammo.emp -= amount;
                    break;
                case AmmunitionManager.SMB_01:
                    Ammo.smb -= amount;
                    break;
                case AmmunitionManager.R_IC3:
                    Ammo.ice -= amount;
                    break;
                case AmmunitionManager.DCR_250:
                    Ammo.dcr -= amount;
                    break;
                case AmmunitionManager.WIZ_X:
                    Ammo.wiz -= amount;
                    break;
                case AmmunitionManager.PLD_8:
                    Ammo.pld -= amount;
                    break;
                case AmmunitionManager.CBO_100:
                    Ammo.cbo100 -= amount;
                    break;
                case AmmunitionManager.JOB_100:
                    Ammo.job100 -= amount;
                    break;
                case AmmunitionManager.RB_214:
                    Ammo.rb214 -= amount;
                    break;
                case AmmunitionManager.CLK_XL:
                    Ammo.cloacks -= amount;
                    break;
                case AmmunitionManager.R_310:
                    Ammo.r310 -= amount;
                    break;
                case AmmunitionManager.PLT_2021:
                    Ammo.plt21 -= amount;
                    break;
                case AmmunitionManager.PLT_2026:
                    Ammo.plt26 -= amount;
                    break;
                case AmmunitionManager.PLT_3030:
                    Ammo.plt3030 -= amount;
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_ECO_10:
                    Ammo.eco10 -= amount;
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_SAR_01:
                    Ammo.sar01 -= amount;
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_HSTRM_01:
                    Ammo.hstrm01 -= amount;
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_SAR_02:
                    Ammo.sar02 -= amount;
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_UBR_100:
                    Ammo.ubr100 -= amount;
                    break;
            }
            SettingsManager.SendNewItemStatus(ammoId);
        }

        public void AddAmmo(string ammoId, int amount)
        {
            string name = "";
            switch (ammoId)
            {
                case AmmunitionManager.LCB_10:
                    Ammo.lcb10 += amount;
                    name = "LCB-10";
                    break;
                case AmmunitionManager.MCB_25:
                    Ammo.mcb25 += amount;
                    name = "MCB-25";
                    break;
                case AmmunitionManager.MCB_50:
                    Ammo.mcb50 += amount;
                    name = "MCB-50";
                    break;
                case AmmunitionManager.UCB_100:
                    Ammo.ucb100 += amount;
                    name = "UCB-100";
                    break;
                case AmmunitionManager.SAB_50:
                    Ammo.sab50 += amount;
                    name = "SAB-50";
                    break;
                case AmmunitionManager.RSB_75:
                    Ammo.rsb75 += amount;
                    name = "RSB-75";
                    break;
                case AmmunitionManager.ISH_01:
                    Ammo.ish += amount;
                    name = "ISH-01";
                    break;
                case AmmunitionManager.EMP_01:
                    Ammo.emp += amount;
                    name = "EMP-01";
                    break;
                case AmmunitionManager.SMB_01:
                    Ammo.smb += amount;
                    name = "SMB-01";
                    break;
                case AmmunitionManager.R_IC3:
                    Ammo.ice += amount;
                    name = "R-IC3";
                    break;
                case AmmunitionManager.DCR_250:
                    Ammo.dcr += amount;
                    name = "DCR-250";
                    break;
                case AmmunitionManager.WIZ_X:
                    Ammo.wiz += amount;
                    name = "WIZ-X";
                    break;
                case AmmunitionManager.PLD_8:
                    Ammo.pld += amount;
                    name = "PLD-8";
                    break;
                case AmmunitionManager.CBO_100:
                    Ammo.cbo100 += amount;
                    name = "CBO-100";
                    break;
                case AmmunitionManager.JOB_100:
                    Ammo.job100 += amount;
                    name = "JOB-100";
                    break;
                case AmmunitionManager.RB_214:
                    Ammo.rb214 += amount;
                    name = "RB-214";
                    break;
                case AmmunitionManager.CLK_XL:
                    Ammo.cloacks += amount;
                    name = "CLKL";
                    break;
                case AmmunitionManager.R_310:
                    Ammo.r310 += amount;
                    name = "R_310";
                    break;
                case AmmunitionManager.PLT_2021:
                    Ammo.plt21 += amount;
                    name = "PLT-2021";
                    break;
                case AmmunitionManager.PLT_2026:
                    Ammo.plt26 += amount;
                    name = "PLT-2026";
                    break;
                case AmmunitionManager.PLT_3030:
                    Ammo.plt3030 += amount;
                    name = "PLT-3030";
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_ECO_10:
                    Ammo.eco10 += amount;
                    name = "SAR-02";
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_SAR_01:
                    Ammo.sar01 += amount;
                    name = "SAR-02";
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_HSTRM_01:
                    Ammo.hstrm01 += amount;
                    name = "HSTRM-01";
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_SAR_02:
                    Ammo.sar02 += amount;
                    name = "SAR-02";
                    break;
                case AmmunitionManager.ROCKET_LAUNCHER_UBR_100:
                    Ammo.ubr100 += amount;
                    name = "SAR-02";
                    break;
            }
            SettingsManager.SendNewItemStatus(ammoId);
            SendPacket($"0|A|STD| You received {amount} {name}");
        }

        public override byte[] GetShipCreateCommand() { return null; }
    }
}

