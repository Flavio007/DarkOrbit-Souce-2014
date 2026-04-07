using Ow.Game.Objects.Players.Managers;
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
            if (Active && cooldown.AddMilliseconds(Duration) < DateTime.Now)
                Disable();
        }

        public override void Send()
        {
            if (CitadelIds.Contains(Player.Ship.Id) && (cooldown.AddMilliseconds(Duration + Cooldown) < DateTime.Now || Player.Storage.GodMode))
            {
                Player.Storage.CitadelFortify = true;
                Player.SendCommand(SetSpeedCommand.write(Player.Speed, Player.Speed));
                Player.AddVisualModifier(VisualModifierCommand.FORTIFY, 0, "", 0, true);

                Player.SendCooldown(LootId, Duration, true);
                Active = true;
                cooldown = DateTime.Now;
            }
        }

        public override void Disable()
        {
            Player.Storage.CitadelFortify = false;
            Player.SendCommand(SetSpeedCommand.write(Player.Speed, Player.Speed));
            Player.RemoveVisualModifier(VisualModifierCommand.FORTIFY);
            Player.SendCooldown(LootId, Cooldown);
            Active = false;
        }
    }
}