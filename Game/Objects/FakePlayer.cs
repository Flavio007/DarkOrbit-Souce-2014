using Ow.Game.Movements;
using Ow.Game.Objects.Players.Managers;
using Ow.Managers;

namespace Ow.Game.Objects
{
    class FakePlayer : Player
    {
        private const int LF3_DAMAGE = 150;
        private const int BO2_SHIELD = 10000;
        private const int BO2_SHIELD_ABSORPTION = 80;

        public bool IsAiControlled { get; private set; }

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

        public override void Tick()
        {
            if (Destroyed) return;

            Movement.ActualPosition(this);
            Storage.Tick();
            RefreshAttackers();
        }
    }
}
