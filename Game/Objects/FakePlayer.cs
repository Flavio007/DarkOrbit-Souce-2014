using Ow.Game.Movements;
using Ow.Game.Objects.AI;
using Ow.Game.Objects.Players;
using Ow.Game.Objects.Players.Managers;
using Ow.Managers;
using System.Collections.Generic;

namespace Ow.Game.Objects
{
    class FakePlayer : Player
    {
        private const int LF3_DAMAGE = 150;
        private const int BO2_SHIELD = 10000;
        private const int BO2_SHIELD_ABSORPTION = 80;

        public bool IsAiControlled { get; private set; }
        public string ConfiguredAbility { get; private set; }

        public FakePlayer(int id, string name, Ship ship, Spacemap spacemap, Position position, int factionId)
            : base(id, name, GameManager.GetClan(0), factionId, 1, 0, ship)
        {
            IsAiControlled = true;
            Spacemap = spacemap;
            Position = new Position(position.X, position.Y);
            Destination = new Position(position.X, position.Y);
            OldPosition = new Position(position.X, position.Y);
            Direction = new Position(position.X, position.Y);
            Moving = false;

            Data = new DataBase();
            Destructions = new DestructionsBase();
            LastPosition = new LastPosition { map = spacemap.Id, x = position.X, y = position.Y };
            ShipStatus = new ShipStatus();
            CurrentConfig = 1;
            Settings.InGameSettings.currentConfig = 1;
            Settings.InGameSettings.selectedFormation = DroneManager.DEFAULT_FORMATION;

            InitializeTestLoadout();

            Spacemap.AddCharacter(this);
            Program.TickManager.AddTick(this);
        }

        private void InitializeTestLoadout()
        {
            const int maxLasersPerConfig = 15;
            const int maxGeneratorsPerConfig = 15;

            var hp = Ship.BaseHitpoints + GetSkillPercentage("Ship Hull");
            var baseSpeed = Ship.BaseSpeed + Ow.Utils.Maths.GetPercentage(Ship.BaseSpeed, 20);
            var configDamage = maxLasersPerConfig * LF3_DAMAGE;
            var configShield = maxGeneratorsPerConfig * BO2_SHIELD;

            Equipment = new EquipmentBase(
                new ConfigsBase(
                    hp,
                    configDamage,
                    configShield,
                    baseSpeed,
                    hp,
                    configDamage,
                    configShield,
                    baseSpeed,
                    configDamage,
                    configDamage,
                    configShield,
                    configShield
                ),
                new ItemsBase(0)
            );

            CurrentShieldAbsConfig1 = BO2_SHIELD_ABSORPTION;
            CurrentShieldAbsConfig2 = BO2_SHIELD_ABSORPTION;
            CurrentHitPoints = MaxHitPoints;
            CurrentShieldPoints = MaxShieldPoints;
            CurrentNanoHull = MaxNanoHull;
            Ammo.lcb10 = 1000000;
            Ammo.mcb25 = 1000000;
            Ammo.mcb50 = 1000000;
            Ammo.ucb100 = 1000000;
            Ammo.r310 = 1000000;
            AttackManager.RocketLauncher.MaxLoad = 0;
            equipedlasercount = maxLasersPerConfig * 2;
            fulllf3 = true;

            if (DroneManager != null)
            {
                DroneManager.DronesList.Clear();
                DroneManager.Config1Designs = new System.Collections.Generic.List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                DroneManager.Config2Designs = new System.Collections.Generic.List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                DroneManager.Apis = false;
                DroneManager.Zeus = false;
            }
        }

        public void ApplyProfile(
            string name,
            Clan clan,
            int rankId,
            int level,
            string ability,
            Dictionary<string, int> availableAmmo,
            int empAmount,
            int ishAmount,
            int smbAmount,
            List<Drones> drones)
        {
            if (!string.IsNullOrWhiteSpace(name))
                Name = name.Trim();

            Clan = clan ?? GameManager.GetClan(0);
            RankId = rankId > 0 ? rankId : RankId;
            Data.experience = GetExperienceForLevel(level);
            ConfiguredAbility = ability ?? "";

            ApplyAvailableAmmo(availableAmmo);
            Ammo.emp = empAmount < 0 ? 0 : empAmount;
            Ammo.ish = ishAmount < 0 ? 0 : ishAmount;
            Ammo.smb = smbAmount < 0 ? 0 : smbAmount;

            if (DroneManager != null)
            {
                DroneManager.DronesList = drones ?? new List<Drones>();
                DroneManager.Config1Designs = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                DroneManager.Config2Designs = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                DroneManager.Apis = DroneManager.DronesList.Exists(x => x != null && x.DroneType == 3);
                DroneManager.Zeus = DroneManager.DronesList.Exists(x => x != null && x.DroneType == 4);
            }

            UpdateStatus();
        }

        private void ApplyAvailableAmmo(Dictionary<string, int> availableAmmo)
        {
            if (availableAmmo == null)
                return;

            foreach (var ammo in availableAmmo)
            {
                var amount = ammo.Value < 0 ? 0 : ammo.Value;
                switch ((ammo.Key ?? "").Trim().ToLowerInvariant())
                {
                    case "lcb10":
                    case "lcb-10":
                    case "ammunition_laser_lcb-10":
                        Ammo.lcb10 = amount;
                        break;
                    case "mcb25":
                    case "mcb-25":
                    case "ammunition_laser_mcb-25":
                        Ammo.mcb25 = amount;
                        break;
                    case "mcb50":
                    case "mcb-50":
                    case "ammunition_laser_mcb-50":
                        Ammo.mcb50 = amount;
                        break;
                    case "ucb100":
                    case "ucb-100":
                    case "ammunition_laser_ucb-100":
                        Ammo.ucb100 = amount;
                        break;
                    case "sab50":
                    case "sab-50":
                    case "ammunition_laser_sab-50":
                        Ammo.sab50 = amount;
                        break;
                    case "rsb75":
                    case "rsb-75":
                    case "ammunition_laser_rsb-75":
                        Ammo.rsb75 = amount;
                        break;
                    case "r310":
                    case "r-310":
                    case "ammunition_rocket_r-310":
                        Ammo.r310 = amount;
                        break;
                    case "plt2026":
                    case "plt-2026":
                    case "ammunition_rocket_plt-2026":
                        Ammo.plt26 = amount;
                        break;
                    case "plt2021":
                    case "plt-2021":
                    case "ammunition_rocket_plt-2021":
                        Ammo.plt21 = amount;
                        break;
                    case "plt3030":
                    case "plt-3030":
                    case "ammunition_rocket_plt-3030":
                        Ammo.plt3030 = amount;
                        break;
                    case "eco10":
                    case "eco-10":
                    case "ammunition_rocketlauncher_eco-10":
                        Ammo.eco10 = amount;
                        break;
                    case "hstrm01":
                    case "hstrm-01":
                    case "ammunition_rocketlauncher_hstrm-01":
                        Ammo.hstrm01 = amount;
                        break;
                    case "ubr100":
                    case "ubr-100":
                    case "ammunition_rocketlauncher_ubr-100":
                        Ammo.ubr100 = amount;
                        break;
                    case "sar01":
                    case "sar-01":
                    case "ammunition_rocketlauncher_sar-01":
                        Ammo.sar01 = amount;
                        break;
                    case "sar02":
                    case "sar-02":
                    case "ammunition_rocketlauncher_sar-02":
                        Ammo.sar02 = amount;
                        break;
                }
            }
        }

        private long GetExperienceForLevel(int level)
        {
            if (level <= 1)
                return 0;

            long threshold = 10000;
            for (var currentLevel = 2; currentLevel < level; currentLevel++)
                threshold *= 2;

            return threshold;
        }

        public override void Tick()
        {
            if (Destroyed) return;

            Movement.ActualPosition(this);
            AIShips.Tick(this);
            AttackManager.LaserAttack();
            AttackManager.RocketLauncher.Tick();
            Storage.Tick();
            RefreshAttackers();
            AttackManager.FlushPendingDamageHits();
        }
    }
}
