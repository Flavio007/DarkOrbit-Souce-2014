using Ow.Game.Objects.Players.Managers;
using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;

namespace Ow.Game.Objects.Players.Skills
{
    class JamX : Skill
    {
        private const int AbilityEffectId = 110;
        private const int Range = 700;
        private static readonly List<int> SpearheadIds = new List<int> { Ship.SPEARHEAD, Ship.SPEARHEAD_VETERAN, Ship.SPEARHEAD_ELITE };

        private readonly List<int> jammedTargetIds = new List<int>();

        public override string LootId { get => SkillManager.SPEARHEAD_JAM_X; }

        public override int Duration { get => TimeManager.SPEARHEAD_JAM_X_DURATION; }
        public override int Cooldown { get => TimeManager.SPEARHEAD_JAM_X_COOLDOWN; }

        public JamX(Player player) : base(player) { }

        public override void Tick()
        {
            if (Active && cooldown.AddMilliseconds(Duration) < DateTime.Now)
                Disable();
        }

        public override void Send()
        {
            if (!SpearheadIds.Contains(Player.Ship.Id) || !(cooldown.AddMilliseconds(Cooldown) < DateTime.Now || Player.Storage.GodMode))
                return;

            jammedTargetIds.Clear();
            var effectTargetIds = new List<int>();

            foreach (var otherCharacter in Player.InRangeCharacters.Values)
            {
                if (!(otherCharacter is Player target))
                    continue;

                if (target.Id == Player.Id || target.FactionId == Player.FactionId)
                    continue;

                if (target.Position.DistanceTo(Player.Position) > Range)
                    continue;

                target.Storage.skillsBlockedUntil = DateTime.Now.AddMilliseconds(Duration);
                target.SkillManager.DisableAllSkills();
                jammedTargetIds.Add(target.Id);
                effectTargetIds.Add(target.Id);
            }

            if (effectTargetIds.Count == 0)
                effectTargetIds.Add(Player.Id);

            var abilityEffectActivationCommand = AbilityEffectActivationCommand.write(AbilityEffectId, Player.Id, effectTargetIds);
            var abilityEffectDeActivationCommand = AbilityEffectDeActivationCommand.write(AbilityEffectId, Player.Id, effectTargetIds);

            Player.SendCommand(abilityEffectActivationCommand);
            Player.SendCommandToInRangePlayers(abilityEffectActivationCommand);
            Player.SendCommand(abilityEffectDeActivationCommand);
            Player.SendCommandToInRangePlayers(abilityEffectDeActivationCommand);

            Player.SendCooldown(LootId, Cooldown);
            Player.CpuManager.DisableCloak();

            Active = true;
            cooldown = DateTime.Now;
        }

        public override void Disable()
        {
            foreach (var jammedTargetId in jammedTargetIds)
            {
                var target = Ow.Managers.GameManager.GetPlayerById(jammedTargetId);
                if (target != null && target.Storage.skillsBlockedUntil < DateTime.Now)
                    target.Storage.skillsBlockedUntil = DateTime.Now;
            }

            jammedTargetIds.Clear();
            Active = false;
        }
    }
}