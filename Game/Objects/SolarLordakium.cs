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
    class SolarLordakium : Npc
    {
        public new NpcAI NpcAI { get; set; }
        public new bool Attacking = false;
        public bool Alive = true;

        public DateTime respawntime;

        public List<Player> challengers = new List<Player>();

        public SolarLordakium(int id, Ship ship, Spacemap spacemap, Position position, int Owner) : base(id, ship, spacemap, position, 0)
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
            MotherShipId = Owner;

            AddVisualModifier(VisualModifierCommand.GREEN_GLOW, 0, "", 0, true);

            Program.TickManager.AddTick(this);
        }

        public override void Tick()
        {
            if (!Destroyed)
            {
                Movement.ActualPosition(this);
                NpcAI.TickAI();
                CheckShieldPointsRepair();
                Storage.Tick();
                RefreshAttackers();
                ProtegitCheck();

                if (Attacking && Damage > 0)
                    Attack();
            }

        }


        public new DateTime lastAttackTime = new DateTime();
        public new void Attack()
        {
            var damage = AttackManager.RandomizeDamage(Damage, (Storage.underPLD8 ? 0.5 : 0.1));
            var target = SelectedCharacter;

            if (!TargetDefinition(target, false)) return;

            if (target is Player player && player.AttackManager.EmpCooldown.AddMilliseconds(TimeManager.EMP_DURATION) > DateTime.Now)
            {
                Selected = null;
                return;
            }

            if (lastAttackTime.AddSeconds(1) < DateTime.Now)
            {
                if (target is Player && (target as Player).Storage.Spectrum)
                    damage -= Maths.GetPercentage(damage, 50);

                int damageShd = 0, damageHp = 0;

                double shieldAbsorb = System.Math.Abs(target.ShieldAbsorption - 0);

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

                if (target is Player && (target as Player).Storage.Sentinel)
                    damageShd -= Maths.GetPercentage(damageShd, 30);

                var laserRunCommand = AttackLaserRunCommand.write(Id, target.Id, 0, false, false);
                SendCommandToInRangePlayers(laserRunCommand);

                if (damage == 0)
                {
                    var attackMissedCommandToInRange = AttackMissedCommand.write(new AttackTypeModule(AttackTypeModule.LASER), target.Id, 1);
                    SendCommandToInRangePlayers(attackMissedCommandToInRange);
                }
                else
                {
                    var attackHitCommand =
                        AttackHitCommand.write(new AttackTypeModule(AttackTypeModule.LASER), Id,
                             target.Id, target.CurrentHitPoints,
                             target.CurrentShieldPoints, target.CurrentNanoHull,
                             damage > damageShd ? damage : damageShd, false);

                    SendCommandToInRangePlayers(attackHitCommand);
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

        public new DateTime lastShieldRepairTime = new DateTime();
        private void CheckShieldPointsRepair()
        {
            if (LastCombatTime.AddSeconds(100) >= DateTime.Now || lastShieldRepairTime.AddSeconds(1) >= DateTime.Now || CurrentShieldPoints == MaxShieldPoints) return;

                    

            int repairShield = MaxShieldPoints / 1;
            CurrentShieldPoints += repairShield;
            UpdateStatus();

            lastShieldRepairTime = DateTime.Now;
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

        public new void ReceiveAttack(Character character)
        {
            Selected = character;
            if (!Attacking)
            {
                Attacking = true;
            }
            if (character is Player player)
            {
                if (!challengers.Contains(player))
                {
                    challengers.Add(player);
                }
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
