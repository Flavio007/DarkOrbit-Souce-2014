using Ow.Game.Objects.Players.Managers;
using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;

namespace Ow.Game.Objects.Players.Skills
{
    class UltimateCloak : Skill
    {
        private const int AbilityEffectId = 103;
        private static readonly List<int> SpearheadIds = new List<int> { Ship.SPEARHEAD, Ship.SPEARHEAD_VETERAN, Ship.SPEARHEAD_ELITE };

        public override string LootId { get => SkillManager.SPEARHEAD_ULTIMATE_CLOAK; }

        public override int Duration { get => TimeManager.SPEARHEAD_ULTIMATE_CLOAK_DURATION; }
        public override int Cooldown { get => TimeManager.SPEARHEAD_ULTIMATE_CLOAK_COOLDOWN; }

        public UltimateCloak(Player player) : base(player) { }

        public override void Tick()
        {
            if (Active && cooldown.AddMilliseconds(Duration) < DateTime.Now)
                Disable();
        }

        public override void Send()
        {
            if (!SpearheadIds.Contains(Player.Ship.Id) || !(cooldown.AddMilliseconds(Duration + Cooldown) < DateTime.Now || Player.Storage.GodMode))
                return;

            if (Player.Spacemap.Options.CloakBlocked)
                return;

            Player.Storage.SpearheadUltimateCloak = true;
            Player.CpuManager.EnableCloak();

            var effectTargetIds = new List<int> { Player.Id };
            var abilityEffectActivationCommand = AbilityEffectActivationCommand.write(AbilityEffectId, Player.Id, effectTargetIds);

            Player.SendCommand(abilityEffectActivationCommand);
            Player.SendCommandToInRangePlayers(abilityEffectActivationCommand);
            Player.SendCooldown(LootId, Duration, true);

            Active = true;
            cooldown = DateTime.Now;
        }

        public override void Disable()
        {
            Player.Storage.SpearheadUltimateCloak = false;
            Player.CpuManager.DisableCloak();

            var effectTargetIds = new List<int> { Player.Id };
            var abilityStopCommand = AbilityStopCommand.write(AbilityEffectId, Player.Id, effectTargetIds);
            var abilityEffectDeActivationCommand = AbilityEffectDeActivationCommand.write(AbilityEffectId, Player.Id, effectTargetIds);

            Player.SendCommand(abilityStopCommand);
            Player.SendCommand(abilityEffectDeActivationCommand);
            Player.SendCommandToInRangePlayers(abilityStopCommand);
            Player.SendCommandToInRangePlayers(abilityEffectDeActivationCommand);

            Player.SendCooldown(LootId, Cooldown);
            Active = false;
        }
    }
}