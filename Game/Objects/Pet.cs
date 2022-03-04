using Ow.Game.Movements;
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
    class Pet : Character
    {
        public Player Owner { get; set; }

        public override int Speed
        {
            get
            {
                return (int)(Owner.Speed * 1.25);
            }
        }

        public bool Activated = false;
        public bool GuardModeActive = false;
        public int level = 15;
        public int xp = 0;
        private double SHDboost = 0;
        private double DMGboost = 0;
        private int kamikazedmg;
        public bool KamikazeModeActive = false;
        public bool KamikazeSelected = false;
        public bool InKamikize = false;
        public bool KamikazeAi = true;
        public bool ComboRepairModeActive = false;
        public bool RepairModeActive = false;
        public bool AutoLootModeActive = false;
        public short GearId = PetGearTypeModule.PASSIVE;

        public Pet(Player player) : base(Randoms.CreateRandomID(), "P.E.T 15", player.FactionId, GameManager.GetShip(player.PetLevel), player.Position, player.Spacemap, player.Clan, player.GetPetExpansion(player.PetLevel))
        {
            Name = player.PetName;
            Owner = player;
            player.PetLevel = level;
            CheckBoosters();

            ShieldAbsorption = 0.8;
            Damage = (int)(5000 * DMGboost);
            CurrentHitPoints = 2500;
            MaxHitPoints = level * 10000 + 50000;
            MaxShieldPoints = (int)(50000 * SHDboost);
            CurrentShieldPoints = MaxShieldPoints;
        }

        public override void Tick()
        {
            if (Activated)
            {
                CheckShieldPointsRepair();
                CheckGuardMode();
                CheckAutoLoot();
                CheckBoosters();
                Follow(Owner);
                CheckKamikazeMode();
                Movement.ActualPosition(this);
            }
        }

        public void CheckBoosters()
        {
            SHDboost = level >= 1 && level < 3 ? 1.02 : level >= 3 && level < 6 ? 1.04 : level >= 6 && level < 9 ? 1.06 : level >= 9 && level < 11 ? 1.08 : level >= 11 && level < 13 ? 1.10 : level >= 13 && level < 15 ? 1.12 : level == 15 ? 1.15 : 0;
            DMGboost = level >= 1 && level < 3 ? 1.02 : level >= 3 && level < 6 ? 1.04 : level >= 6 && level < 9 ? 1.06 : level >= 9 && level < 11 ? 1.08 : level >= 11 && level < 13 ? 1.10 : level >= 13 ? 1.12 : 0;
        }

        public void CheckAutoLoot()
        {
            //TODO
        }

        public DateTime Kamikazecdr = new DateTime();
        public void InKami()
        {
            if (!KamikazeModeActive)
                if (level < 4)
                    if (Kamikazecdr.AddSeconds(TimeManager.G_KK1_COOLDOWN) >= DateTime.Now)
                    {
                        Owner.SendPacket("0|A|STD|Kamikaze is now available again!");
                        KamikazeModeActive = true;
                    }
                if (level < 7)
                    if (Kamikazecdr.AddSeconds(TimeManager.G_KK2_COOLDOWN) >= DateTime.Now)
                    {
                        Owner.SendPacket("0|A|STD|Kamikaze is now available again!");
                        KamikazeModeActive = true;
                    }
                else
                    if (Kamikazecdr.AddSeconds(TimeManager.G_KK3_COOLDOWN) >= DateTime.Now)
                    {
                        Owner.SendPacket("0|A|STD|Kamikaze is now available again!");
                        KamikazeModeActive = true;
                    }
        }
        public void CheckKamikazeMode()
        {
            if (KamikazeModeActive)
            {
                if (Owner.AttackingOrUnderAttack(5) && Owner.MainAttacker != null)
                {
                    Kamikaze(Owner.MainAttacker);
                }
                else
                {
                    foreach (var target in Owner.InRangeCharacters.Values)
                    {
                        if (target.Selected == Owner && Owner.AttackingOrUnderAttack(3))
                        {
                            Kamikaze(target);
                        }
                    }
                }
            }
        }
        private void Kamikaze(Character target)
        {

            if (Kamikazecdr == DateTime.Now)
            {
                FollowEnemyAsync(target);
            }
            else if (Kamikazecdr.AddSeconds(30) <= DateTime.Now)
            {
                FollowEnemyAsync(target);
            }
            else
            {
                GearId = PetGearTypeModule.GUARD;
                Owner.SendCommand(PetGearSelectCommand.write(new PetGearTypeModule(PetGearTypeModule.GUARD), new List<int>()));
            }


        }

        private async Task FollowEnemyAsync(Character target)
        {
            if (KamikazeAi && !InKamikize)
            {


                var startkami = DateTime.Now;
                InKamikize = true;

                Owner.Selected = target;

                DateTime start = DateTime.Now;
                GameManager.SendPacketToAll($"0|n|fx|start|RAGE|{Id}");
                int timer = 0;
                bool wait = true;
                for (int i = 0; i < 5; i++)
                {
                    if (i == 0)
                    {
                        timer = 1300;
                    }
                    else if (i > 0)
                    {
                        timer = 1200;
                    }

                    if (Owner.Speed > 450)
                    {
                        timer += 250;
                    }
                    Movement.Move(this, target.Position);
                    if (i >= 2 && Position.DistanceTo(target.Position) < 150)
                    {
                        Explode(target);
                        wait = false;
                        break;

                    }
                    if (wait)
                        await Task.Delay(1000);


                }
                if (wait)
                    Explode(target);
            }

        }

        public void Explode(Character target)
        {
            kamikazedmg = level < 4 ? 25000 : level < 8 ? 45000 : 75000;

            foreach (var character in this.InRangeCharacters.Where(n => Position.DistanceTo(n.Value.Position) < 1000))
            {
                if (character.Value != Owner)
                {

                    if (character.Value is Player && !(character.Value as Player).Attackable())
                    {
                        kamikazedmg = 0;
                        Damage = 0;

                    }
                    var distance = Position.DistanceTo(character.Value.Position);
                    if (distance > 400)
                    {
                        if (distance < 850)
                        {
                            kamikazedmg = Maths.GetPercentage(kamikazedmg, 85);
                            kamikazedmg = AttackManager.RandomizeDamage(kamikazedmg, 0);
                        }
                        else
                        {
                            kamikazedmg = Maths.GetPercentage(kamikazedmg, 65);
                            kamikazedmg = AttackManager.RandomizeDamage(kamikazedmg, 0);
                        }
                    }

                    character.Value.CurrentHitPoints -= Maths.GetPercentage(kamikazedmg, 95);
                    character.Value.CurrentShieldPoints -= Maths.GetPercentage(kamikazedmg, 5);
                    if (character.Value.CurrentHitPoints - Maths.GetPercentage(kamikazedmg, 95) <= 0 || character.Value.CurrentHitPoints <= 0)
                    {
                        character.Value.Destroy(Owner, DestructionType.PLAYER);
                    }
                    character.Value.UpdateStatus();
                    var attackHitCommand =
                            AttackHitCommand.write(new AttackTypeModule(AttackTypeModule.KAMIKAZE), Id,
                                                    character.Value.Id, character.Value.CurrentHitPoints,
                                                        character.Value.CurrentShieldPoints, character.Value.CurrentNanoHull,
                                                    Damage > kamikazedmg ? Damage : kamikazedmg, false);

                    SendCommandToInRangePlayers(attackHitCommand);

                }
            }

            Destroy(this, DestructionType.PET);
            InKamikize = false;
            KamikazeAi = false;
            target = null;
            Kamikazecdr = DateTime.Now;


        }


        public DateTime lastShieldRepairTime = new DateTime();
        private void CheckShieldPointsRepair()
        {
            if (LastCombatTime.AddSeconds(10) >= DateTime.Now || lastShieldRepairTime.AddSeconds(1) >= DateTime.Now || CurrentShieldPoints == MaxShieldPoints) return;

            int repairShield = MaxShieldPoints / 25;
            CurrentShieldPoints += repairShield;
            UpdateStatus();

            lastShieldRepairTime = DateTime.Now;
        }

        public DateTime lastAttackTime = new DateTime();
        public DateTime lastRSBAttackTime = new DateTime();
        public void CheckGuardMode()
        {
            if (GuardModeActive)
            {
                foreach (var enemy in Owner.InRangeCharacters.Values)
                {
                    if (Owner.SelectedCharacter != null && Owner.SelectedCharacter != this)
                    {
                        if ((Owner.AttackingOrUnderAttack(5) || Owner.LastAttackTime(5)) || ((enemy is Player && (enemy as Player).LastAttackTime(5)) && enemy.SelectedCharacter == Owner))
                            Attack(Owner.SelectedCharacter);
                    }
                    else if (enemy is Player)
                    {
                        if ((enemy as Player).LastAttackTime(5) && enemy.SelectedCharacter == Owner)
                            Attack(enemy);
                    }
                    else if (enemy is Npc)
                    {
                        if ((enemy as Npc).Attacking && enemy.InRange(Owner,500) && enemy.SelectedCharacter == Owner)
                            Attack(enemy);
                    }
                }
            }
        }

        private int GetLaserDamage()
        {
            switch(Owner.AttackManager.GetSelectedLaser())
            {
                case 0:
                    return 1;
                case 1:
                    return 2;
                case 2:
                    return 3;
                case 3:
                    return 4;
                case 4:
                    return 2;
                case 6:
                    return 6;
                default:
                    return 1;
            }
        }

        private void Attack(Character target)
        {
            if (!Owner.TargetDefinition(target, false)) return;
            if ((Owner.Settings.InGameSettings.selectedLaser == AmmunitionManager.RSB_75 ? lastRSBAttackTime : lastAttackTime).AddSeconds(Owner.Settings.InGameSettings.selectedLaser == AmmunitionManager.RSB_75 ? 3 : 1) < DateTime.Now)
            {
                int damageShd = 0, damageHp = 0;
                int tempdmg = Damage * GetLaserDamage();

                if (target is Spaceball)
                {
                    var spaceball = target as Spaceball;
                    spaceball.AddDamage(this, tempdmg);
                }

                double shieldAbsorb = System.Math.Abs(target.ShieldAbsorption - 1);

                if (shieldAbsorb > 1)
                    shieldAbsorb = 1;

                if ((target.CurrentShieldPoints - tempdmg) >= 0)
                {
                    damageShd = (int)(tempdmg * shieldAbsorb);
                    damageHp = tempdmg - damageShd;
                }
                else
                {
                    int newDamage = tempdmg - target.CurrentShieldPoints;
                    damageShd = target.CurrentShieldPoints;
                    damageHp = (int)(newDamage + (damageShd * shieldAbsorb));
                }

                if ((target.CurrentHitPoints - damageHp) < 0)
                {
                    damageHp = target.CurrentHitPoints;
                }

                if (target is Player && !(target as Player).Attackable())
                {
                    tempdmg = 0;
                    damageShd = 0;
                    damageHp = 0;
                }

                if (Invisible)
                {
                    Invisible = false;
                    string cloakPacket = "0|n|INV|" + Id + "|0";
                    SendPacketToInRangePlayers(cloakPacket);
                }

                if (target is Player && (target as Player).Storage.Sentinel)
                    damageShd -= Maths.GetPercentage(damageShd, 30);

                var laserRunCommand = AttackLaserRunCommand.write(Id, target.Id, Owner.AttackManager.GetSelectedLaser(), false, false);
                SendCommandToInRangePlayers(laserRunCommand);

                var attackHitCommand =
                        AttackHitCommand.write(new AttackTypeModule(AttackTypeModule.LASER), Id,
                                                target.Id, target.CurrentHitPoints,
                                                target.CurrentShieldPoints, target.CurrentNanoHull,
                                                tempdmg > damageShd ? tempdmg : damageShd, false);

                SendCommandToInRangePlayers(attackHitCommand);

                if (damageHp >= target.CurrentHitPoints || target.CurrentHitPoints == 0)
                    target.Destroy(this, DestructionType.PET);
                else
                    target.CurrentHitPoints -= damageHp;

                target.CurrentShieldPoints -= damageShd;
                target.LastCombatTime = DateTime.Now;

                if (Owner.Settings.InGameSettings.selectedLaser == AmmunitionManager.RSB_75)
                    lastRSBAttackTime = DateTime.Now;
                else
                    lastAttackTime = DateTime.Now;

                target.UpdateStatus();
            }
        }

        public void Activate()
        {
            if (!Activated && !Owner.Settings.InGameSettings.petDestroyed)
            {
                Activated = true;

                CurrentHitPoints = 2500;

                SetPosition(Owner.Position);
                Spacemap = Owner.Spacemap;
                Invisible = Owner.Invisible;

                Owner.SendPacket("0|A|STM|msg_pet_activated");

                Initialization(GearId);

                Spacemap.AddCharacter(this);
                Program.TickManager.AddTick(this);
            }
            else
            {
                Deactivate();
            }
        }

        public void RepairDestroyed()
        {
            if (Owner.Settings.InGameSettings.petDestroyed)
            {
                var cost = Owner.Premium ? 0 : 250;

                if (Owner.Data.uridium >= cost)
                {
                    Destroyed = false;
                    Owner.ChangeData(DataType.URIDIUM, cost, ChangeType.DECREASE);
                    Owner.SendCommand(PetRepairCompleteCommand.write());
                    Owner.Settings.InGameSettings.petDestroyed = false;
                    QueryManager.SavePlayer.Settings(Owner, "inGameSettings", Owner.Settings.InGameSettings);
                } else Owner.SendPacket("0|A|STM|ttip_pet_repair_disabled_through_money");
            }
        }

        public void Deactivate(bool direct = false, bool destroyed = false)
        {
            if (Activated)
            {
                if (LastCombatTime.AddSeconds(10) < DateTime.Now || direct)
                {
                    Owner.SendPacket("0|PET|D");

                    if (destroyed)
                    {
                        Owner.Settings.InGameSettings.petDestroyed = true;
                        QueryManager.SavePlayer.Settings(Owner, "inGameSettings", Owner.Settings.InGameSettings);

                        Owner.SendPacket("0|PET|Z");
                        CurrentShieldPoints = 0;
                        UpdateStatus();

                        Owner.SendCommand(PetInitializationCommand.write(true, true, false));
                        Owner.SendCommand(PetUIRepairButtonCommand.write(true, 250));
                    }
                    else Owner.SendPacket("0|A|STM|msg_pet_deactivated");

                    Activated = false;

                    Deselection();
                    Spacemap.RemoveCharacter(this);
                    InRangeCharacters.Clear();
                    Program.TickManager.RemoveTick(this);
                }
                else
                {
                    Owner.SendPacket("0|A|STM|msg_pet_in_combat");
                }
            }
        }

        private void Initialization(short gearId = PetGearTypeModule.PASSIVE)
        {
            Owner.SendCommand(PetStatusCommand.write(Id, level, xp, 27000000, CurrentHitPoints, MaxHitPoints, CurrentShieldPoints, MaxShieldPoints, 50000, 50000, Speed, Name));
            Owner.SendCommand(PetGearAddCommand.write(new PetGearTypeModule(PetGearTypeModule.PASSIVE), 0, 0, true));
            Owner.SendCommand(PetGearAddCommand.write(new PetGearTypeModule(PetGearTypeModule.GUARD), 0, 0, true));
            Owner.SendCommand(PetGearAddCommand.write(new PetGearTypeModule(PetGearTypeModule.KAMIKAZE), 0, 0, true));
            SwitchGear(gearId);
        }

        private void Follow(Character character)
        {
            var distance = Position.DistanceTo(character.Position);
            if (distance < 450 && character.Moving) return;

            if (character.Moving)
            {
                Movement.Move(this, character.Position);
            }
            else if (Math.Abs(distance - 300) > 250 && !Moving)
                Movement.Move(this, Position.GetPosOnCircle(character.Position, 250));
        }

        public void SwitchGear(short gearId)
        {
            if (!Activated)
                Activate();

            switch (gearId)
            {
                case PetGearTypeModule.PASSIVE:
                    GuardModeActive = false;
                    break;
                case PetGearTypeModule.GUARD:
                    GuardModeActive = true;
                    break;
                case PetGearTypeModule.KAMIKAZE:
                    KamikazeModeActive = true;
                    GuardModeActive = false;
                    break;
            }
            GearId = gearId;

            Owner.SendCommand(PetGearSelectCommand.write(new PetGearTypeModule(gearId), new List<int>()));
        }

        public override byte[] GetShipCreateCommand() { return null; }
    }
}
