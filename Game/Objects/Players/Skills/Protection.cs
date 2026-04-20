using Ow.Game.Objects.Players.Managers;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Game.Objects.Players.Skills
{
    class Protection : Skill
    {
        private const int AbilityEffectId = 108;
        private const int RedirectPercentage = 25;
        private const int Range = 700;
        private static readonly List<int> CitadelIds = new List<int> { Ship.CITADEL, Ship.CITADEL_VETERAN, Ship.CITADEL_ELITE, Ship.CITADEL_PLUS };

        private readonly List<int> protectedTargetIds = new List<int>();

        public override string LootId { get => SkillManager.CITADEL_PROTECTION; }

        public override int Duration { get => TimeManager.CITADEL_PROTECTION_DURATION; }
        public override int Cooldown { get => TimeManager.CITADEL_PROTECTION_COOLDOWN; }

        public Protection(Player player) : base(player) { }

        public override void Tick()
        {
            if (Active && cooldown.AddMilliseconds(Duration) < DateTime.Now)
                Disable();
        }

        public override void Send()
        {
            if (CitadelIds.Contains(Player.Ship.Id) && (cooldown.AddMilliseconds(Duration + EffectiveCooldown) < DateTime.Now || Player.Storage.GodMode))
            {
                Active = true;
                protectedTargetIds.Clear();

                if (Player.Group != null)
                {
                    foreach (var member in Player.Group.Members.Values.Where(member => member != null && member.Id != Player.Id))
                    {
                        if (member.Spacemap.Id != Player.Spacemap.Id || member.Position.DistanceTo(Player.Position) > Range)
                            continue;

                        member.Storage.DeactiveProtectionEffect();
                        member.Storage.underProtection = true;
                        member.Storage.underProtectionTime = DateTime.Now;
                        member.Storage.protectedByCitadelId = Player.Id;
                        member.AddVisualModifier(VisualModifierCommand.PROTECTION_TARGET, 0, "", 0, true);
                        protectedTargetIds.Add(member.Id);
                    }
                }

                var effectTargetIds = new List<int> { Player.Id };
                var abilityEffectActivationCommand = AbilityEffectActivationCommand.write(AbilityEffectId, Player.Id, effectTargetIds);

                Player.SendCommand(abilityEffectActivationCommand);
                Player.SendCommandToInRangePlayers(abilityEffectActivationCommand);

                Player.SendCooldown(LootId, Duration, true);
                Player.CpuManager.DisableCloak();
                cooldown = DateTime.Now;
            }
        }

        public override void Disable()
        {
            foreach (var targetId in protectedTargetIds)
            {
                var protectedPlayer = GameManager.GetPlayerById(targetId);

                if (protectedPlayer?.Storage != null && protectedPlayer.Storage.protectedByCitadelId == Player.Id)
                    protectedPlayer.Storage.DeactiveProtectionEffect();
            }

            protectedTargetIds.Clear();

            var effectTargetIds = new List<int> { Player.Id };
            var abilityStopCommand = AbilityStopCommand.write(AbilityEffectId, Player.Id, effectTargetIds);
            var abilityEffectDeActivationCommand = AbilityEffectDeActivationCommand.write(AbilityEffectId, Player.Id, effectTargetIds);

            Player.SendCommand(abilityStopCommand);
            Player.SendCommand(abilityEffectDeActivationCommand);
            Player.SendCommandToInRangePlayers(abilityStopCommand);
            Player.SendCommandToInRangePlayers(abilityEffectDeActivationCommand);

            Player.SendCooldown(LootId, EffectiveCooldown);
            Active = false;
        }

        public static bool TryRedirectDamage(Player attacker, Player target, DamageType damageType, ref int damage, double shieldPenetration)
        {
            if (attacker == null || target == null || damage <= 0 || !target.Storage.underProtection || target.Storage.protectedByCitadelId <= 0)
                return false;

            var protector = GameManager.GetPlayerById(target.Storage.protectedByCitadelId);

            if (protector == null || protector.Destroyed || protector.Id == target.Id)
            {
                target.Storage.DeactiveProtectionEffect();
                return false;
            }

            if (protector.Spacemap.Id != target.Spacemap.Id || protector.Group == null || target.Group != protector.Group || protector.Position.DistanceTo(target.Position) > Range)
            {
                target.Storage.DeactiveProtectionEffect();
                return false;
            }

            if (!protector.Storage.Skills.TryGetValue(SkillManager.CITADEL_PROTECTION, out var skill) || !skill.Active)
            {
                target.Storage.DeactiveProtectionEffect();
                return false;
            }

            var redirectedDamage = Maths.GetPercentage(damage, RedirectPercentage);
            if (redirectedDamage <= 0)
                return false;

            damage -= redirectedDamage;
            attacker.AttackManager?.Damage(attacker, protector, damageType, redirectedDamage, shieldPenetration, false);
            return true;
        }
    }
}