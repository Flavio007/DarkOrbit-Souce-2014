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
        public List<Protegit> protegits = new List<Protegit>();
        public int MinionShip = 1;
        public int WaveSize = 1;
        public bool Alive = true;
        private Position respawnpos;
        public DateTime respawntime;
        private readonly ClassicCubikonWave classicWave = new ClassicCubikonWave();
        private readonly object classicWaveSync = new object();


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
            checkrepsawn();
            if (Destroyed) return;
            Movement.ActualPosition(this);
            TickClassicWave();
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

                (target as Player)?.Quests?.RecordDamage(Math.Max(0, Math.Min(damageHp, target.CurrentHitPoints)));
            if (damageHp >= target.CurrentHitPoints || target.CurrentHitPoints <= 0)
                {
                    target.Destroy(this, DestructionType.NPC);
                    lastAttackTime = DateTime.Now;
                    return;
                }
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
            if (Ship.Id == 80)
            {
                lock (classicWaveSync)
                    if (classicWave.PendingCount == 0 && !Destroyed && Alive)
                        classicWave.Begin(npcid, Math.Min(Math.Max(0, count - 1), Math.Max(0, 20 - minioncount)), DateTime.Now);
                return;
            }

            if (Ship.Id == 480 || Ship.Id == 880)
            {
                AddVisualModifier(VisualModifierCommand.SKULL, 1, "", 1, true);
                GameManager.SendPacketToMap(Spacemap.Id, $"0|n|s|start|{Id}");
            }

            for (int i = 1; i < count; i++)
                if (minioncount < 20)
                {
                    var boss = Ship.Id == 480 || Ship.Id == 880;
                    var origin = boss ? new Position(Position.X, Position.Y) : Position.GetPosOnCircle(Position, 10);
                    var git = new Protegit(Randoms.CreateRandomID(), GameManager.GetShip(npcid), Spacemap, origin, this);
                    protegits.Add(git);
                    if (boss)
                    {
                        // Spread the wave evenly from the center in the game's world coordinates.
                        var angle = 2 * Math.PI * (i - 1) / Math.Max(1, count - 1);
                        var destination = new Position(Position.X + (int)Math.Round(300 * Math.Cos(angle)),
                            Position.Y + (int)Math.Round(300 * Math.Sin(angle)));
                        Movement.Move(git, destination);
                    }
                    minioncount++;
                }
        }

        // Original door transforms + the git's exit point at frame 45 (world art scale 1).
        private static readonly Position[] ClassicExits =
        {
            new Position(-172, 64),
            new Position(-169, 41),
            new Position(-163, 13),
            new Position(-158, -18),
            new Position(-152, -50),
            new Position(-147, -90),
            new Position(137, 105),
            new Position(159, 77),
            new Position(183, 49),
            new Position(211, 19)
        };

        private void TickClassicWave()
        {
            if (Ship.Id != 80) return;
            lock (classicWaveSync)
            {
                if (Destroyed || !Alive) { classicWave.Cancel(); return; }
                classicWave.Tick(DateTime.Now,
                    door => GameManager.SendPacketToMap(Spacemap.Id, $"0|n|s|door|{Id}|{door}"),
                    (ship, door) =>
                    {
                        if (minioncount >= 20) return;
                        var exit = ClassicExits[door];
                        var git = new Protegit(Randoms.CreateRandomID(), GameManager.GetShip(ship), Spacemap,
                            new Position(Position.X + exit.X, Position.Y + exit.Y), this);
                        protegits.Add(git);
                        minioncount++;
                    });
            }
        }

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
            lock (classicWaveSync) classicWave.Cancel();
            RespawnAt(respawn);
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
                Convert.ToString(Ship.GetDisplayShipId()),
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
