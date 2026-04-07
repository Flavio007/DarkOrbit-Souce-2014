using Ow.Game.Objects.Players.Managers;
using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;

namespace Ow.Game.Objects.Players.Skills
{
    class Recon : Skill
    {
        private const int AbilityEffectId = 111;
        private static readonly List<int> SpearheadIds = new List<int> { Ship.SPEARHEAD, Ship.SPEARHEAD_VETERAN, Ship.SPEARHEAD_ELITE };

        public override string LootId { get => SkillManager.SPEARHEAD_DOUBLE_MINIMAP; }

        public override int Duration { get => TimeManager.SPEARHEAD_RECON_DURATION; }
        public override int Cooldown { get => TimeManager.SPEARHEAD_RECON_COOLDOWN; }

        public Recon(Player player) : base(player) { }

        public override void Tick()
        {
            if (Active && cooldown.AddMilliseconds(Duration) < DateTime.Now)
                Disable();
        }

        public override void Send()
        {
            if (!SpearheadIds.Contains(Player.Ship.Id) || !(cooldown.AddMilliseconds(Duration + Cooldown) < DateTime.Now || Player.Storage.GodMode))
                return;

            Player.Storage.SpearheadRecon = true;

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
            Player.Storage.SpearheadRecon = false;

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