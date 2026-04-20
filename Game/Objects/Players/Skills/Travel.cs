using Ow.Game.Objects.Players.Managers;
using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;

namespace Ow.Game.Objects.Players.Skills
{
    class Travel : Skill
    {
        private const int AbilityEffectId = 107;
        private static readonly List<int> CitadelIds = new List<int> { Ship.CITADEL, Ship.CITADEL_VETERAN, Ship.CITADEL_ELITE, Ship.CITADEL_PLUS };

        public override string LootId { get => SkillManager.CITADEL_TRAVEL; }

        public override int Duration { get => TimeManager.CITADEL_TRAVEL_DURATION; }
        public override int Cooldown { get => TimeManager.CITADEL_TRAVEL_COOLDOWN; }

        public Travel(Player player) : base(player) { }

        public override void Tick()
        {
            if (Active && cooldown.AddMilliseconds(Duration) < DateTime.Now)
                Disable();
        }

        public override void Send()
        {
            if (CitadelIds.Contains(Player.Ship.Id) && (cooldown.AddMilliseconds(Duration + EffectiveCooldown) < DateTime.Now || Player.Storage.GodMode))
            {
                Player.Storage.CitadelTravel = true;
                Player.SendCommand(SetSpeedCommand.write(Player.Speed, Player.Speed));

                var effectTargetIds = new List<int> { Player.Id };
                var abilityEffectActivationCommand = AbilityEffectActivationCommand.write(AbilityEffectId, Player.Id, effectTargetIds);

                Player.SendCommand(abilityEffectActivationCommand);
                Player.SendCommandToInRangePlayers(abilityEffectActivationCommand);

                Player.SendCooldown(LootId, Duration, true);
                Player.CpuManager.DisableCloak();
                Active = true;
                cooldown = DateTime.Now;
            }
        }

        public override void Disable()
        {
            Player.Storage.CitadelTravel = false;
            Player.SendCommand(SetSpeedCommand.write(Player.Speed, Player.Speed));

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
    }
}