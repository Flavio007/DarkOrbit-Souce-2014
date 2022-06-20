using Ow.Game.Movements;
using Ow.Game.Objects.AI;
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
    class Escort : Npc
    {
        public InstanceNpc Owner { get; set; }
        public string Suffix = "";
        public List<Player> challengers = new List<Player>();
        public Escort(int id, Ship ship, Spacemap spacemap, Position position, float multiplier, string suffix, InstanceNpc VIP) : base(id, ship, spacemap, position, 0)
        {
            Spacemap.AddCharacter(this);

            ShieldAbsorption = 0.5;

            Damage = ship.Damage;
            MaxHitPoints = ship.BaseHitpoints;
            CurrentHitPoints = MaxHitPoints;
            MaxShieldPoints = ship.BaseShieldPoints;
            CurrentShieldPoints = MaxShieldPoints;
            Suffix = suffix;

            NpcAI = new NpcAI(this);
            NpcAI.AIOption = NpcAIOption.ESCORT;
            NpcAI.VIP = VIP;
            Owner = VIP;
            Ship.Respawnable = false;
            Ship.Aggressive = true;

            Damage = Convert.ToInt32(ship.Damage * multiplier);
            MaxHitPoints = Convert.ToInt32(ship.BaseHitpoints * multiplier);
            CurrentHitPoints = Convert.ToInt32(MaxHitPoints * multiplier);
            MaxShieldPoints = Convert.ToInt32(ship.BaseShieldPoints * multiplier);
            CurrentShieldPoints = Convert.ToInt32(MaxShieldPoints * multiplier);

            Credits = Convert.ToInt32(Ship.Rewards.Credits * multiplier);
            Uridium = Convert.ToInt32(Ship.Rewards.Uridium * multiplier);
            Honor = Convert.ToInt32(Ship.Rewards.Honor * multiplier);
            Experience = Convert.ToInt32(Ship.Rewards.Experience * multiplier);

            Program.TickManager.AddTick(this);
        }

        public new void ReceiveAttack(Character character)
        {
            Selected = character;
            if (!Attacking)
            {
                Attacking = true;
            }
            if (character is Player player)
            {
                if (!challengers.Contains(player))
                {
                    challengers.Add(player);
                }
            }
        }

        public override byte[] GetShipCreateCommand()
        {
            return ShipCreateCommand.write(
                Id,
                Convert.ToString(Ship.Id),
                3,
                "",
                Ship.Name + Suffix,
                Position.X,
                Position.Y,
                FactionId,
                0,
                0,
                false,
                new ClanRelationModule(ClanRelationModule.AT_WAR),
                0,
                false,
                true,
                false,
                ClanRelationModule.AT_WAR,
                ClanRelationModule.AT_WAR,
                new List<VisualModifierCommand>(),
                new class_11d(class_11d.DEFAULT)
                );
        }
    }
}

