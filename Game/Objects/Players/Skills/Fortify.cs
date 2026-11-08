using Ow.Game.Objects.Players.Managers;
using Ow.Game.Movements;
using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;

namespace Ow.Game.Objects.Players.Skills
{
    class Fortify : Skill
    {
        private static readonly List<int> CitadelIds = new List<int> { Ship.CITADEL, Ship.CITADEL_VETERAN, Ship.CITADEL_ELITE, Ship.CITADEL_PLUS };

        public override string LootId { get => SkillManager.CITADEL_FORTIFY; }

        public override int Duration { get => TimeManager.CITADEL_FORTIFY_DURATION; }
        public override int Cooldown { get => TimeManager.CITADEL_FORTIFY_COOLDOWN; }

        public Fortify(Player player) : base(player) { }

        public override void Tick()
        {
            Skill current;
            if (!Player.Storage.Skills.TryGetValue(LootId, out current) || !ReferenceEquals(current, this))
                return;
            if (Active && cooldown.AddMilliseconds(Duration) < DateTime.Now)
                Disable();
            if (!Active && CitadelIds.Contains(Player.Ship.Id) &&
                (cooldown.AddMilliseconds(Duration + EffectiveCooldown) < DateTime.Now || Player.Storage.GodMode))
                Player.AddVisualModifier(VisualModifierCommand.HEALING_POD, 0, "", 0, true);
        }

        public override void Send()
        {
            if (CitadelIds.Contains(Player.Ship.Id) && (cooldown.AddMilliseconds(Duration + EffectiveCooldown) < DateTime.Now || Player.Storage.GodMode))
            {
                lock (Player)
                {
                    Player.Storage.CitadelFortify = true;
                    var stoppedAt = Movement.Stop(Player);
                    Player.RemoveVisualModifier(VisualModifierCommand.HEALING_POD);
                    Player.SendCommand(MoveCommand.write(Player.Id, stoppedAt.X, stoppedAt.Y, 0));
                    Player.SendCommandToInRangePlayers(MoveCommand.write(Player.Id, stoppedAt.X, stoppedAt.Y, 0));
                    Player.SendCommand(SetSpeedCommand.write(Player.Speed, Player.Speed));
                    Player.AddVisualModifier(VisualModifierCommand.FORTIFY, 0, "", 0, true);

                    Player.SendCooldown(LootId, Duration, true);
                    Active = true;
                    cooldown = DateTime.Now;
                }
            }
        }

        public override void Disable()
        {
            Player.Storage.CitadelFortify = false;
            Player.SendCommand(SetSpeedCommand.write(Player.Speed, Player.Speed));
            Player.RemoveVisualModifier(VisualModifierCommand.FORTIFY);
            Player.SendCooldown(LootId, EffectiveCooldown);
            Active = false;
        }
    }
}