using Ow.Game.Movements;
using Ow.Game.Objects.AI;
using Ow.Game.Objects.Collectables;
using Ow.Game.Objects.Players;
using Ow.Game.Objects.Players.Managers;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects
{
    class Cubikon : Npc
    {
        public new NpcAI NpcAI { get; set; }
        public new bool Attacking = false;
        public new int MotherShipId = 0;
        public new int minioncount = 0;
        public List<Protegit> protegits = new List<Protegit>();
        public int MinionShip = 1;
        public int WaveSize = 1;
        public bool Alive = true;
        private Position respawnpos;
        public DateTime respawntime;


        public Cubikon(int id, Ship ship, Spacemap spacemap, Position position, int minionsShip, int minionsCount) : base(id, ship, spacemap, position, 0)
        {
            Spacemap.AddCharacter(this);

            ShieldAbsorption = 0.5;

            Damage = ship.Damage;
            MaxHitPoints = ship.BaseHitpoints;
            CurrentHitPoints = MaxHitPoints;
            MaxShieldPoints = ship.BaseShieldPoints;
            CurrentShieldPoints = MaxShieldPoints;

            NpcAI = new NpcAI(this);
            NpcAI.RespawnX = Position.X;
            NpcAI.RespawnY = Position.Y;

            respawnpos = position;

            MinionShip = minionsShip;
            WaveSize = minionsCount;

        Program.TickManager.AddTick(this);
        }

        public override void Tick()
        {
            Movement.ActualPosition(this);
            NpcAI.TickAI();
            CheckShieldPointsRepair();
            Storage.Tick();
            RefreshAttackers();
            checkrepsawn();

            if (Attacking && Damage > 0)
                Attack();
        }

        void checkrepsawn()
        {
            if (!Alive && respawntime <= DateTime.Now)
            {
                Respawn(respawnpos);
                Alive = true;
            }
        }


        public new DateTime lastAttackTime = new DateTime();
        public new void Attack()
        {
            var target = SelectedCharacter;

            if (!TargetDefinition(target, false)) return;

            if (target is Player player && player.AttackManager.EmpCooldown.AddMilliseconds(TimeManager.EMP_DURATION) > DateTime.Now)
                return;

            if (lastAttackTime.AddSeconds(1) < DateTime.Now)
            {

                int damageShd = 0, damageHp = 0;

                if ((target.CurrentHitPoints - damageHp) < 0)
                {
                    damageHp = target.CurrentHitPoints;
                }

                if (target is Player && !(target as Player).Attackable())
                {
                    damageShd = 0;
                    damageHp = 0;
                }

                if (damageHp >= target.CurrentHitPoints || target.CurrentHitPoints <= 0)
                    target.Destroy(this, DestructionType.NPC);
                else
                    target.CurrentHitPoints -= damageHp;

                target.CurrentShieldPoints -= damageShd;
                target.LastCombatTime = DateTime.Now;

                lastAttackTime = DateTime.Now;

                target.UpdateStatus();
            }
        }

        public void SpawnWave(int npcid, int count)
        {
            if (Ship.Id == 480 || Ship.Id == 880)
            {
                AddVisualModifier(VisualModifierCommand.SKULL, 5, "", 0, true);
                GameManager.SendPacketToMap(Spacemap.Id, $"0|n|s|start|{Id}");
            }

            for (int i = 1; i < count; i++)
                if (minioncount < 20)
                {
                    protegits.Add(new Protegit(Randoms.CreateRandomID(), GameManager.GetShip(npcid), Spacemap, Position.GetPosOnCircle(Position, 500), this));
                    minioncount++;
                }
        }

        public new DateTime lastShieldRepairTime = new DateTime();
        private void CheckShieldPointsRepair()
        {
            if (LastCombatTime.AddSeconds(10) >= DateTime.Now || lastShieldRepairTime.AddSeconds(1) >= DateTime.Now || CurrentShieldPoints == MaxShieldPoints) return;
                if (Ship.Id == 480 || Ship.Id == 880)
                    GameManager.SendPacketToMap(Spacemap.Id, $"0|n|s|end|{Id}");
                    

            int repairShield = MaxShieldPoints / 10;
            CurrentShieldPoints += repairShield;
            UpdateStatus();
            if(LastCombatTime.AddSeconds(10) >= DateTime.Now)
                minioncount = 0;

            lastShieldRepairTime = DateTime.Now;
        }

        public void Respawn(Position respawn)
        {
            LastCombatTime = DateTime.Now.AddSeconds(-999);
            CurrentHitPoints = MaxHitPoints;
            CurrentShieldPoints = MaxShieldPoints;
            SetPosition(respawn);
            Spacemap.AddCharacter(this);
            Attackers.Clear();
            MainAttacker = null;
            Destroyed = false;
            protegits.Clear();
            minioncount = 0;
        }

        void CheckWaveSize()
        {
            if (Attacking)
            {
                if (minioncount == 0)
                {
                    SpawnWave(MinionShip, WaveSize);
                }
                else if (minioncount < WaveSize / 2)
                {
                    SpawnWave(MinionShip, WaveSize / 2);
                }
            }
        }

        public new void ReceiveAttack(Character character)
        {
            Selected = character;
            if (!Attacking)
            {
                Attacking = true;
            }
            foreach(Protegit git in protegits)
            {
                if(!git.underAttack)
                    git.FocusAttack(character);
            }
            CheckWaveSize();
        }

        public void DeleteGits(Protegit DeleteThis)
        {
            foreach(Protegit gits in protegits)
            {
                if (gits == DeleteThis)
                {
                    //protegits.Remove(gits);
                    minioncount--;
                }
            }
        }

        public override int Speed
        {
            get
            {
                var value = Ship.BaseSpeed;

                if (Storage.underR_IC3)
                    value -= value;

                return value;
            }
        }

        public override byte[] GetShipCreateCommand()
        {
            return ShipCreateCommand.write(
                Id,
                Convert.ToString(Ship.Id),
                3,
                "",
                Ship.Name,
                Position.X,
                Position.Y,
                FactionId,
                0,
                0,
                false,
                new ClanRelationModule(ClanRelationModule.AT_WAR),
                0,
                false,
                true,
                false,
                ClanRelationModule.AT_WAR,
                ClanRelationModule.AT_WAR,
                new List<VisualModifierCommand>(),
                new class_11d(class_11d.DEFAULT)
                );
        }
    }
}
