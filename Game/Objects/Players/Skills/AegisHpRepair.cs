using Ow.Game.Objects.Players.Managers;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects.Players.Skills
{
    class AegisHpRepair : Skill
    {
        public override string LootId { get => SkillManager.AEGIS_HP_REPAIR; }

        public override int Duration { get => TimeManager.AEGIS_HP_REPAIR_DURATION; }

        public override int Cooldown
        {
            get
            {
                var value = TimeManager.AEGIS_HP_REPAIR_COOLDOWN;

                if (Player.Ship.Id == Ship.AEGIS_ELITE)
                    value -= Maths.GetPercentage(value, 10);

                return value;
            }
        }

        public AegisHpRepair(Player player) : base(player) { }

        public List<int> targetIds = new List<int>();

        public override void Tick()
        {
            if (Active)
            {
                if (cooldown.AddMilliseconds(Duration) < DateTime.Now)
                    Disable();
                else
                    ExecuteHeal();
            }
        }

        public override void Send()
        {
            var aegisIds = new List<int> { Ship.AEGIS, Ship.AEGIS_VETERAN, Ship.AEGIS_ELITE };

            if (aegisIds.Contains(Player.Ship.Id) && (cooldown.AddMilliseconds(Duration + EffectiveCooldown) < DateTime.Now || Player.Storage.GodMode))
            {
                targetIds.Clear();
                Active = true;

                var target = Player.Selected;

                if (target != null)
                {
                    short relationType = Player.Clan.Id != 0 && target.Clan.Id != 0 ? Player.Clan.GetRelation(target.Clan) : (short)0;

                    if ((Player.Group != null && Player.Group.Members.ContainsKey(target.Id)) || relationType != ClanRelationModule.AT_WAR)
                        targetIds.Add(target.Id);
                }

                var visualTargets = new List<int>(targetIds) { Player.Id };
                var activation = AbilityEffectActivationCommand.write(101, Player.Id, visualTargets);
                Player.SendCommand(activation);
                Player.SendCommandToInRangePlayers(activation);

                Player.SendCooldown(LootId, Duration, true);
                Player.CpuManager.DisableCloak();

                cooldown = DateTime.Now;
            }
        }

        public override void Disable()
        {
            Active = false;
            var visualTargets = new List<int>(targetIds) { Player.Id };
            var stop = AbilityStopCommand.write(101, Player.Id, visualTargets);
            var deactivation = AbilityEffectDeActivationCommand.write(101, Player.Id, visualTargets);
            Player.SendCommand(stop);
            Player.SendCommand(deactivation);
            Player.SendCommandToInRangePlayers(stop);
            Player.SendCommandToInRangePlayers(deactivation);
            targetIds.Clear();
            Player.SendCooldown(LootId, EffectiveCooldown);
        }

        public DateTime HealTime = new DateTime();
        public void ExecuteHeal()
        {
            if (HealTime.AddSeconds(1) < DateTime.Now)
            {
                Player.Heal(20000);

                foreach (var id in targetIds)
                {
                    var player = GameManager.GetPlayerById(id);

                    if (player != null && player.Position.DistanceTo(Player.Position) < 500)
                    {
                        player.Heal(40000, Player.Id);
                    }
                }

                HealTime = DateTime.Now;
            }
        }
    }
}
