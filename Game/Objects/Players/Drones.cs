using System;
using Newtonsoft.Json;

namespace Ow.Game.Objects.Players
{
    class Drones
    {
        public int Id { set; get; }

        [JsonProperty("Type")]
        public byte DroneType { set; get; }

        [JsonProperty("Exp")]
        public int Experience { set; get; }

        [JsonProperty("Dmg")]
        public int Damage { set; get; }

        [JsonProperty("Lvl")]
        public int Level { set; get; }

        public Drones()
        {
        }

        public Drones(int id, byte dronetype, int experiece, int damage, int level)
        {
            Id = id;
            DroneType = dronetype;
            Experience = experiece;
            Damage = damage;
            Level = level;
        }
    }
}
