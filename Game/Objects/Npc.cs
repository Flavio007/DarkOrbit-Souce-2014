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
    class Npc : Character
    {
        private const int CenturyFalconWaveSize = 20;
        private const int CenturyFalconLaserId = 3;
        private const int CenturyFalconLaserMultiplier = 4;
        private const int CenturyFalconRocketLauncherId = 7;
        private const int CenturyFalconRocketCount = 10;
        private const int CenturyFalconRocketDamage = 4000;
        private const int CenturyFalconRocketCooldownSeconds = 20;
        private readonly List<Protegit> centuryFalconMinions = new List<Protegit>();

        public NpcAI NpcAI { get; set; }
        public bool Attacking = false;
        public bool Aggressive = false;
        public bool Respawnable = true;
        public bool UseMapWideChaseRange = false;
        //public bool Minion = false;
        public int MotherShipId = 0;
        public int minioncount = 0;
        public int AgroRange = 500;

        public Npc(int id, Ship ship, Spacemap spacemap, Position position, int Owner) : base(id, ship.Name, 0, ship, position, spacemap, GameManager.GetClan(0), 0)
        {
            Spacemap.AddCharacter(this);

            ShieldAbsorption = 0.5;

            Damage = ship.Damage;
            MaxHitPoints = ship.BaseHitpoints;
            CurrentHitPoints = MaxHitPoints;
            MaxShieldPoints = ship.BaseShieldPoints;
            CurrentShieldPoints = MaxShieldPoints;
            Aggressive = ship.Aggressive;
            Respawnable = ship.Respawnable;

            NpcAI = new NpcAI(this);
            NpcAI.RespawnX = Position.X;
            NpcAI.RespawnY = Position.Y;
            MotherShipId = Owner;

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


        public DateTime lastAttackTime = new DateTime();
        public DateTime lastRocketLauncherAttackTime = new DateTime();
        public void Attack()
        {
            var target = SelectedCharacter;

            if (!TargetDefinition(target, false)) return;

            if (target is Player player && player.AttackManager.EmpCooldown.AddMilliseconds(TimeManager.EMP_DURATION) > DateTime.Now)
            {
                Selected = null;
                return;
            }

            if (IsCenturyFalcon)
            {
                if (lastAttackTime.AddSeconds(1) < DateTime.Now)
                {
                    var laserDamage = AttackManager.RandomizeDamage(Damage * CenturyFalconLaserMultiplier, (Storage.underPLD8 ? 0.5 : 0.1));
                    ApplyAttack(target, laserDamage, DamageType.LASER, false, () =>
                    {
                        var laserRunCommand = AttackLaserRunCommand.write(Id, target.Id, CenturyFalconLaserId, false, false);
                        SendCommandToInRangePlayers(laserRunCommand);
                    });
                }

                if (SelectedCharacter != null && !SelectedCharacter.Destroyed && lastRocketLauncherAttackTime.AddSeconds(CenturyFalconRocketCooldownSeconds) < DateTime.Now)
                    AttackCenturyFalcon(SelectedCharacter);

                return;
            }

            if (lastAttackTime.AddSeconds(1) < DateTime.Now)
            {
                var damage = AttackManager.RandomizeDamage(Damage, (Storage.underPLD8 ? 0.5 : 0.1));
                ApplyAttack(target, damage, DamageType.LASER, false, () =>
                {
                    var laserRunCommand = AttackLaserRunCommand.write(Id, target.Id, 0, false, false);
                    SendCommandToInRangePlayers(laserRunCommand);
                });
            }
        }

        private void AttackCenturyFalcon(Character target)
        {
            var totalDamage = 0;
            for (var rocketIndex = 0; rocketIndex < CenturyFalconRocketCount; rocketIndex++)
                totalDamage += AttackManager.RandomizeDamage(CenturyFalconRocketDamage, (Storage.underPLD8 ? 0.5 : 0.1));

            ApplyAttack(target, totalDamage, DamageType.ROCKET, true, () =>
            {
                var rocketLauncherPacket = $"0|RL|A|{Id}|{target.Id}|{CenturyFalconRocketCount}|{CenturyFalconRocketLauncherId}";
                SendPacketToInRangePlayers(rocketLauncherPacket);
            });

            lastRocketLauncherAttackTime = DateTime.Now;
        }

        private void ApplyAttack(Character target, int damage, DamageType damageType, bool sendMissedPacket, Action sendAttackVisual)
        {
            if (target is Player targetPlayer && targetPlayer.Storage.Spectrum)
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
                damageHp = target.CurrentHitPoints;

            if (target is Player player && !player.Attackable())
            {
                damage = 0;
                damageShd = 0;
                damageHp = 0;
            }

            if (target is Player shieldedPlayer && shieldedPlayer.Storage.Sentinel)
                damageShd -= Maths.GetPercentage(damageShd, 30);

            damageHp = target.ClampHitpointDamage(damageHp);
            damage = damageHp + damageShd;

            sendAttackVisual?.Invoke();

            if (damage == 0)
            {
                if (sendMissedPacket)
                    SendMissedAttack(target, damageType);
            }
            else
            {
                var attackHitDamage = damage > damageShd ? damage : damageShd;
                var attackHitCommand =
                    AttackHitCommand.write(new AttackTypeModule((short)damageType), Id,
                        target.Id, target.CurrentHitPoints,
                        target.CurrentShieldPoints, target.CurrentNanoHull,
                        attackHitDamage, false);

                foreach (var character in InRangeCharacters.Values)
                    if (character is Player inRangePlayer && (!(target is Player currentTargetPlayer) || inRangePlayer.Id != currentTargetPlayer.Id))
                        inRangePlayer.SendCommand(attackHitCommand);

                if (target is Player attackedPlayer)
                    attackedPlayer.AttackManager.QueueIncomingDamageHit(Id, damageType, attackHitDamage);
            }

            if (damageHp >= target.CurrentHitPoints || (target.CurrentHitPoints <= 0 && target.MinimumHitpoints <= 0))
            {
                if (target is Player attackedPlayer)
                    attackedPlayer.AttackManager.FlushPendingIncomingDamageHitsFromAttacker(Id);

                target.Destroy(this, DestructionType.NPC);
            }
            else
                target.CurrentHitPoints -= damageHp;

            target.CurrentShieldPoints -= damageShd;
            target.LastCombatTime = DateTime.Now;

            lastAttackTime = DateTime.Now;

            target.UpdateStatus();
        }

        private void SendMissedAttack(Character target, DamageType damageType)
        {
            var missedAttackForTarget = AttackMissedCommand.write(new AttackTypeModule((short)damageType), target.Id, 0);
            var missedAttackForInRangePlayers = AttackMissedCommand.write(new AttackTypeModule((short)damageType), target.Id, 1);

            foreach (var character in InRangeCharacters.Values)
            {
                if (!(character is Player inRangePlayer))
                    continue;

                if (target is Player targetPlayer && inRangePlayer.Id == targetPlayer.Id)
                    inRangePlayer.SendCommand(missedAttackForTarget);
                else
                    inRangePlayer.SendCommand(missedAttackForInRangePlayers);
            }
        }

        public void SpawnWave(int owner, int npcid, int count)
        {
            for (int i = 1; i < count; i++)
                new Npc(Randoms.CreateRandomID(), GameManager.GetShip(npcid), this.Spacemap, this.Position, owner);
            minioncount++;
        }

        private bool IsCenturyFalcon => Ship != null && Ship.Id == Ship.CENTURY_FALCON;

        private void SpawnCenturyFalconWave(int count)
        {
            if (!IsCenturyFalcon)
                return;

            for (int index = 1; index < count; index++)
            {
                if (minioncount >= CenturyFalconWaveSize)
                    break;

                var vagrantShip = GameManager.GetShip(Ship.VAGRANT_NPC);
                if (vagrantShip == null)
                    break;

                var minion = new Protegit(Randoms.CreateRandomID(), vagrantShip, Spacemap, Position.GetPosOnCircle(Position, 500), this);
                centuryFalconMinions.Add(minion);
                minioncount++;
            }
        }

        private void CheckCenturyFalconWave(Character target)
        {
            if (!IsCenturyFalcon)
                return;

            centuryFalconMinions.RemoveAll(minion => minion == null || minion.Destroyed);

            if (minioncount == 0)
                SpawnCenturyFalconWave(CenturyFalconWaveSize);
            else if (minioncount < CenturyFalconWaveSize / 2)
                SpawnCenturyFalconWave(CenturyFalconWaveSize / 2);

            foreach (var minion in centuryFalconMinions)
            {
                if (minion != null && !minion.Destroyed && !minion.underAttack)
                    minion.FocusAttack(target);
            }
        }

        public void DeleteGits(Protegit protegit)
        {
            if (!IsCenturyFalcon)
                return;

            if (centuryFalconMinions.Remove(protegit))
                minioncount = Math.Max(0, minioncount - 1);
        }

        public DateTime lastShieldRepairTime = new DateTime();
        private void CheckShieldPointsRepair()
        {
            if (LastCombatTime.AddSeconds(10) >= DateTime.Now || lastShieldRepairTime.AddSeconds(1) >= DateTime.Now || CurrentShieldPoints == MaxShieldPoints) return;



            int repairShield = MaxShieldPoints / 10;
            CurrentShieldPoints += repairShield;
            UpdateStatus();

            lastShieldRepairTime = DateTime.Now;
        }

        public void Respawn()
        {
            LastCombatTime = DateTime.Now.AddSeconds(-999);
            CurrentHitPoints = MaxHitPoints;
            CurrentShieldPoints = MaxShieldPoints;
            SetPosition(Position.Random(Spacemap, 0, Spacemap.Id == 29 ? 41600 : 20800, 0, Spacemap.Id == 29 ? 25600 : 12800));
            Spacemap.AddCharacter(this);
            Attackers.Clear();
            MainAttacker = null;
            Destroyed = false;
            centuryFalconMinions.Clear();
            minioncount = 0;
        }

        public void ProtegitCheck()
        {
            if (this is Protegit git)
            {
                var mother = git.GetMother();
                if (mother == null)
                    return;

                if (git.CubikonAlive && mother.LastCombatTime.AddSeconds(20) <= DateTime.Now || git.lastAttackTime.AddSeconds(10) <= DateTime.Now && !git.CubikonAlive)
                {
                    if (git.Mother != null)
                        git.Mother.DeleteGits(git);
                    else
                        mother.DeleteGits(git);

                    Spacemap.RemoveCharacter(git);
                    git.Destroyed = true;
                }
            }
        }

        public void ReceiveAttack(Character character)
        {
            Selected = character;
            Attacking = true;

            if (IsCenturyFalcon)
                CheckCenturyFalconWave(character);
        }

        public override int Speed
        {
            get
            {
                var value = NpcSpecialBehavior.ResolveSpeed(Ship, SelectedCharacter, Attacking, Ship.BaseSpeed);

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

    internal static class NpcSpecialBehavior
    {
        public static int ResolveSpeed(Ship ship, Character target, bool attacking, int defaultSpeed)
        {
            if (ship != null && ship.Id == Ship.VAGRANT_NPC && attacking && target != null && !target.Destroyed)
                return target.Speed + 15;

            return defaultSpeed;
        }
    }
}

