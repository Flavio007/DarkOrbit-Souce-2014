using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects.Players
{
    class Drones
    {
        public int Id { set; get; }
        public byte DroneType { set; get; }
        public int Experiece { set; get; }
        public int Damage { set; get; }
        public int Level { set; get; }
        public Drones(int id, byte dronetype, int experiece, int damage, int level)
        {
            Id = id;
            DroneType = dronetype;
            Experiece = experiece;
            Damage = damage;
            Level = level;
        }
    }
}
