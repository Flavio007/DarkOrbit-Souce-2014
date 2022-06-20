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
    class InstanceNpc : Npc
    {
        public string Suffix = "";
        public List<Escort> Minions = new List<Escort>();
        public bool KeyNpc = false;
        public List<Player> challengers = new List<Player>();
        public InstanceNpc(int id, Ship ship, Spacemap spacemap, Position position, int Owner, float multiplier, string suffix, bool Key) : base(id, ship, spacemap, position, Owner)
        {
            Spacemap.AddCharacter(this);

            ShieldAbsorption = 0.5;

            Ship.Respawnable = false;
            Suffix = suffix;
            AllMapRange = true;
            Ship.Aggressive = true;
            KeyNpc = Key;

            Damage = Convert.ToInt32(ship.Damage * multiplier);
            MaxHitPoints = Convert.ToInt32(ship.BaseHitpoints * multiplier);
            CurrentHitPoints = Convert.ToInt32(MaxHitPoints * multiplier);
            MaxShieldPoints = Convert.ToInt32(ship.BaseShieldPoints * multiplier);
            CurrentShieldPoints = Convert.ToInt32(MaxShieldPoints * multiplier);

            Credits = Convert.ToInt32(Ship.Rewards.Credits * multiplier);
            Uridium = Convert.ToInt32(Ship.Rewards.Uridium * multiplier);
            Honor = Convert.ToInt32(Ship.Rewards.Honor * multiplier);
            Experience = Convert.ToInt32(Ship.Rewards.Experience * multiplier);


            NpcAI = new NpcAI(this);
            NpcAI.RespawnX = Position.X;
            NpcAI.RespawnY = Position.Y;
            MotherShipId = Owner;

            AgroRange = 20000;
            Check();
        }

        public void Check()
        {
            if (Minions.Count > 0)
            {
                Invincible = true;
                AddVisualModifier(VisualModifierCommand.INVINCIBILITY, 0, "", 0, true);
            }
            else if (Minions.Count == 0)
            {
                Invincible = false;
                RemoveVisualModifier(VisualModifierCommand.INVINCIBILITY);
            }
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