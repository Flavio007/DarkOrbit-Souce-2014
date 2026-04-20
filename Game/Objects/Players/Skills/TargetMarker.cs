using Ow.Game.Objects.Players.Managers;
using Ow.Managers;
using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;

namespace Ow.Game.Objects.Players.Skills
{
    class TargetMarker : Skill
    {
        private const int AbilityEffectId = 106;
        private static readonly List<int> SpearheadIds = new List<int> { Ship.SPEARHEAD, Ship.SPEARHEAD_VETERAN, Ship.SPEARHEAD_ELITE };
        private DateTime lastEffectRefresh = new DateTime();

        private int markedTargetId;

        public override string LootId { get => SkillManager.SPEARHEAD_TARGET_MARKER; }

        public override int Duration { get => TimeManager.SPEARHEAD_TARGET_MARKER_DURATION; }
        public override int Cooldown { get => TimeManager.SPEARHEAD_TARGET_MARKER_COOLDOWN; }

        public TargetMarker(Player player) : base(player) { }

        public override void Tick()
        {
            if (Active && cooldown.AddMilliseconds(Duration) < DateTime.Now)
                Disable();

            if (Active && markedTargetId > 0 && lastEffectRefresh.AddSeconds(1) < DateTime.Now)
                RefreshEffect();
        }

        public override void Send()
        {
            if (!SpearheadIds.Contains(Player.Ship.Id) || !(cooldown.AddMilliseconds(Duration + EffectiveCooldown) < DateTime.Now || Player.Storage.GodMode))
                return;

            var target = Player.Selected as Character;
            if (target == null || !Player.TargetDefinition(target))
                return;

            if (target is Player targetPlayer)
                targetPlayer.Storage.DeactiveSpearheadTargetMarkerEffect();

            markedTargetId = target.Id;
            Active = true;

            if (target is Player selectedPlayer)
            {
                selectedPlayer.Storage.underSpearheadTargetMarker = true;
                selectedPlayer.Storage.underSpearheadTargetMarkerTime = DateTime.Now;
                selectedPlayer.Storage.markedBySpearheadId = Player.Id;
            }

            RefreshEffect();
            Player.SendCooldown(LootId, Duration, true);
            Player.CpuManager.DisableCloak();

            cooldown = DateTime.Now;
        }

        private void RefreshEffect()
        {
            if (markedTargetId <= 0)
                return;

            var effectTargetIds = new List<int> { markedTargetId };
            var abilityEffectActivationCommand = AbilityEffectActivationCommand.write(AbilityEffectId, Player.Id, effectTargetIds);

            Player.SendCommand(abilityEffectActivationCommand);
            Player.SendCommandToInRangePlayers(abilityEffectActivationCommand);
            lastEffectRefresh = DateTime.Now;
        }

        public override void Disable()
        {
            if (markedTargetId > 0)
            {
                if (Player.Spacemap.Characters.TryGetValue(markedTargetId, out var target))
                {
                    if (target is Player targetPlayer)
                        targetPlayer.Storage.DeactiveSpearheadTargetMarkerEffect();
                }

                var effectTargetIds = new List<int> { markedTargetId };
                var abilityStopCommand = AbilityStopCommand.write(AbilityEffectId, Player.Id, effectTargetIds);
                var abilityEffectDeActivationCommand = AbilityEffectDeActivationCommand.write(AbilityEffectId, Player.Id, effectTargetIds);

                Player.SendCommand(abilityStopCommand);
                Player.SendCommand(abilityEffectDeActivationCommand);
                Player.SendCommandToInRangePlayers(abilityStopCommand);
                Player.SendCommandToInRangePlayers(abilityEffectDeActivationCommand);
            }

            markedTargetId = 0;
            lastEffectRefresh = new DateTime();
            Player.SendCooldown(LootId, EffectiveCooldown);
            Active = false;
        }
    }
}