using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ow.Game.Ticks;
using Ow.Managers;
using Ow.Managers.MySQLManager;
using Ow.Game.Objects;
using Ow.Game.Movements;

namespace Ow.Game.Events
{
    class Waves
    {
        public int Id { get; set; }
        public int NpcId { get; set; }
        public int NpcCount { get; set; }
        public int Multiplier { get; set; }
    }

    class Map
    {
        public int Id { get; set; }
        public int FactionId { get; set; }
        public int Dificulty { get; set; }
    }

    class InvasionGate
    {
        public List<Waves> waves = new List<Waves>();
        public static Spacemap Spacemap = GameManager.GetSpacemap(61);
        public int InvasionId = 1;
        public int FactionId = 1;
        public int CurWave = 1;
        public int Points = 0;
        public List<Portal> Portals = new List<Portal>();
        public void Startup()
        {
            GameManager.SendPacketToAll($"0|n|isi|0|0|0"); // Initialize ig window
            GameManager.SendPacketToAll($"0|n|isc|{FactionId}|{CurWave}"); // Update the Rankings
            GameManager.SendPacketToAll($"0|n|isw|{CurWave}"); // Current WAVE
            var sql = SqlDatabaseManager.GetClient();
            for (int y = 1; y < 3; y++)
            {

                Portals.Add(new Portal(GameManager.GetSpacemap(1), Position.InvasionGatePosition, Position.InvasionGatePosition, Spacemap.Id, 41, 0, true, true, false));
                for (int i = 1; i < 10; i++)
                {
                    var querySet = sql.ExecuteQueryRow($"SELECT * FROM server_instanceswaves WHERE GateID = {InvasionId} AND WaveID = {i}");
                    var wave = new Waves();
                    waves.Add(wave);
                    wave.Id = Convert.ToInt32(querySet["WaveID"]);
                    wave.NpcId = Convert.ToInt32(querySet["NpcID"]);
                    wave.NpcCount = Convert.ToInt32(querySet["Count"]);
                    wave.Multiplier = Convert.ToInt32(querySet["Multiplier"]);
                }
            }
        }
    }
}