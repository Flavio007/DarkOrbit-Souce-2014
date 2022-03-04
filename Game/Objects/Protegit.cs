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

/*TODO: Fix the memnory leak*/

namespace Ow.Game.Objects
{
    class Protegit : Npc
    {
        public new NpcAI NpcAI { get; set; }

        public new bool Attacking = false;

        public new bool Minion = false;

        public new int MotherShipId = 0;

        public new int minioncount = 0;

        public bool underAttack = false;

        public Cubikon Mother;

        public bool CubikonAlive = false;

        public Protegit(int id, Ship ship, Spacemap spacemap, Position position, Cubikon Owner) : base(id, ship, spacemap, position, 0)
        {
            Spacemap.AddCharacter(this);

            ShieldAbsorption = 0.5;

            Damage = ship.Damage;
            MaxHitPoints = ship.BaseHitpoints;
            CurrentHitPoints = MaxHitPoints;
            MaxShieldPoints = ship.BaseShieldPoints;
            CurrentShieldPoints = MaxShieldPoints;

            NpcAI = new NpcAI(this);
            NpcAI.RespawnX = Position.X;
            NpcAI.RespawnY = Position.Y;
            Mother = Owner;

            CubikonAlive = true;

            lastAttackTime = DateTime.Now.AddSeconds(2);

            

        Program.TickManager.AddTick(this);
        }

        public void FocusAttack(Character target)
        {
            Selected = target;
        }

        public new void ReceiveAttack(Character character)
        {
            Selected = character;
            Attacking = true;
            underAttack = true;
        }

        public override int Speed
        {
            get
            {
                var value = Ship.BaseSpeed;

                if (Storage.underR_IC3)
                    value -= value;

                return value;
            }
        }

        public override byte[] GetShipCreateCommand()
        {
            return ShipCreateCommand.write(
                Id,
                Convert.ToString(Ship.Id),
                3,
                "",
                Ship.Name,
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
