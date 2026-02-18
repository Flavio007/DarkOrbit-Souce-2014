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
using System.Threading;
using Ow.Utils;

namespace Ow.Game.Events
{
    class Waves
    {
        public int Id { get; set; }
        public int NpcId { get; set; }
        public int NpcCount { get; set; }
        public int Multiplier { get; set; }
        public int KeyNpc { get; set; }
        public int MinionsID { get; set; }
        public int MinionsCount { get; set; }
        public int MinionsMultiplier { set; get; }
    }

    class InvasionGate
    {
        public bool Started = false;
        public int MmoScore = 0;
        public int EicScore = 0;
        public int VruScore = 0;
        public int CurrentWave = 1;
        public List<Waves> waves = new List<Waves>();
        public static Spacemap SpacemapMMO1 = GameManager.GetSpacemap(61);
        public static Spacemap SpacemapEIC1 = GameManager.GetSpacemap(62);
        public static Spacemap SpacemapVRU1 = GameManager.GetSpacemap(63);
        public static Spacemap SpacemapMMO2 = GameManager.GetSpacemap(64);
        public static Spacemap SpacemapEIC2 = GameManager.GetSpacemap(65);
        public static Spacemap SpacemapVRU2 = GameManager.GetSpacemap(66);
        public static Spacemap SpacemapMMO3 = GameManager.GetSpacemap(67);
        public static Spacemap SpacemapEIC3 = GameManager.GetSpacemap(68);
        public static Spacemap SpacemapVRU3 = GameManager.GetSpacemap(69);
        public List<Spacemap> Maps = new List<Spacemap>();
        public int InvasionId = 1;
        public int FactionId = 1;
        public List<Portal> Portals = new List<Portal>();
        public List<int> PointsCounter = new List<int>();
        public List<int> WavesCounter = new List<int>();

        private void BroadcastInvasionInit(int mmoScore, int eicScore, int vruScore, int wave)
        {
            MmoScore = mmoScore;
            EicScore = eicScore;
            VruScore = vruScore;
            CurrentWave = wave;
            GameManager.SendPacketToAll($"0|n|{Ow.Net.netty.ServerCommands.INIT_INVASION_SCOREBOARD}|{mmoScore}|{eicScore}|{vruScore}|{wave}");
        }

        private void BroadcastInvasionScore(int factionId, int score)
        {
            if (factionId == 1) MmoScore = score;
            else if (factionId == 2) EicScore = score;
            else if (factionId == 3) VruScore = score;
            GameManager.SendPacketToAll($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_SCORE}|{factionId}|{score}");
        }

        private void BroadcastInvasionWave(int wave)
        {
            CurrentWave = wave;
            GameManager.SendPacketToAll($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_WAVE}|{wave}");
        }

        public void SendWindowState(Player player)
        {
            if (player == null) return;

            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.INIT_INVASION_SCOREBOARD}|{MmoScore}|{EicScore}|{VruScore}|{CurrentWave}");
            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_SCORE}|1|{MmoScore}");
            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_SCORE}|2|{EicScore}");
            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_SCORE}|3|{VruScore}");
            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_WAVE}|{CurrentWave}");
        }

        public void Startup()
        {
            if (Started) return;
            Started = true;

            BroadcastInvasionInit(0, 0, 0, 1);
            BroadcastInvasionScore(1, 0);
            BroadcastInvasionScore(2, 0);
            BroadcastInvasionScore(3, 0);
            BroadcastInvasionWave(1);

            foreach (var sesion in GameManager.GameSessions.Values)
            {
                var player = sesion.Player;
                player.SettingsManager.SendMenuBarsCommand();
                SendWindowState(player);
            }


            Portals.Add(new Portal(GameManager.GetSpacemap(1), Position.InvasionGatePosition, Position.InvasionGatePosition, SpacemapMMO1.Id, 41, 0, true, true, false));
            Portals.Add(new Portal(GameManager.GetSpacemap(3), Position.InvasionGatePosition, Position.InvasionGatePosition, SpacemapMMO2.Id, 42, 0, true, true, false));
            Portals.Add(new Portal(GameManager.GetSpacemap(17), Position.InvasionGatePosition, Position.InvasionGatePosition, SpacemapMMO3.Id, 43, 0, true, true, false));
            Portals.Add(new Portal(GameManager.GetSpacemap(5), Position.InvasionGatePosition, Position.InvasionGatePosition, SpacemapEIC1.Id, 41, 0, true, true, false));
            Portals.Add(new Portal(GameManager.GetSpacemap(7), Position.InvasionGatePosition, Position.InvasionGatePosition, SpacemapEIC2.Id, 42, 0, true, true, false));
            Portals.Add(new Portal(GameManager.GetSpacemap(21), Position.InvasionGatePosition, Position.InvasionGatePosition, SpacemapEIC3.Id, 43, 0, true, true, false));
            Portals.Add(new Portal(GameManager.GetSpacemap(9), Position.InvasionGatePosition, Position.InvasionGatePosition, SpacemapVRU1.Id, 41, 0, true, true, false));
            Portals.Add(new Portal(GameManager.GetSpacemap(11), Position.InvasionGatePosition, Position.InvasionGatePosition, SpacemapVRU2.Id, 42, 0, true, true, false));
            Portals.Add(new Portal(GameManager.GetSpacemap(25), Position.InvasionGatePosition, Position.InvasionGatePosition, SpacemapVRU3.Id, 43, 0, true, true, false));

            Maps.Add(SpacemapMMO1);
            Maps.Add(SpacemapMMO2);
            Maps.Add(SpacemapMMO3);
            Maps.Add(SpacemapEIC1);
            Maps.Add(SpacemapEIC2);
            Maps.Add(SpacemapEIC3);
            Maps.Add(SpacemapVRU1);
            Maps.Add(SpacemapVRU2);
            Maps.Add(SpacemapVRU3);

            WavesCounter.Add(0);
            WavesCounter.Add(0);
            WavesCounter.Add(0);


            for (int i = 0; i < 9; i++)
            {
                Maps[i].Instance = true;
                Maps[i].Curwave = 0;
            }

            //GameManager.SendChatSystemMessage("Invasion Started!");


            foreach (Portal gates in Portals)
            {
                GameManager.SendCommandToMap(gates.Spacemap.Id, gates.GetAssetCreateCommand());
            }


            var sql = SqlDatabaseManager.GetClient();
            for (int i = 1; i <= 22; i++)
            {
                //GameManager.SendChatSystemMessage($"Getting sql querry ID {i}");
                var querySet = sql.ExecuteQueryRow($"SELECT * FROM server_instanceswaves WHERE GateID = {InvasionId} AND WaveID = {i}");
                var wave = new Waves();
                waves.Add(wave);
                wave.Id = Convert.ToInt32(querySet["WaveID"]);
                wave.NpcId = Convert.ToInt32(querySet["NpcID"]);
                wave.NpcCount = Convert.ToInt32(querySet["Count"]);
                wave.Multiplier = Convert.ToInt32(querySet["Multiplier"]);
                wave.KeyNpc = Convert.ToInt32(querySet["KeyNpc"]);
                wave.MinionsID = Convert.ToInt32(querySet["MinionsID"]);
                wave.MinionsCount = Convert.ToInt32(querySet["MinionsCount"]);
                wave.MinionsMultiplier = Convert.ToInt32(querySet["MinionsMultiplier"]);
            }
            for (int i = 5; i > 0; i--)
            {
                foreach (Spacemap map in Maps)
                {
                    GameManager.SendPacketToMap(map.Id, $"0|A|STD|Level Invasion Gate Starting in {i} Seconds!");
                }
                Thread.Sleep(1000);
            }
            foreach (Spacemap map in Maps)
            {
                //GameManager.SendChatSystemMessage("Starting wave 1");
                StartWave(map, waves[0]);
            }
            Running();
        }

        public void Running()
        {
            while (true)
            {
                if (Started)
                {
                    //GameManager.SendChatSystemMessage("Tick Tock");
                    foreach (Spacemap map in Maps)
                    {
                        int Count = 0;
                        for (int i = 0; i < map.InstanceNpcs.Count; i++)
                        {
                            if (map.InstanceNpcs[i].Destroyed)
                            {
                                Count++;
                            }
                        }
                        CheckWaveFinished(map, Count, waves[map.Curwave]);
                    }
                }
                Thread.Sleep(5000);
            }
        }

        public void CheckWaveFinished(Spacemap map, int NpcCount, Waves wave)
        {
            if (NpcCount >= wave.NpcCount)
            {
                //GameManager.SendChatSystemMessage("Cheking Wave");
                if (map.Curwave <= 21)
                    map.Curwave++;
                else
                    map.Curwave = 1;
                WavesCounter[map.FactionId-1]++;
                map.InstanceNpcs.Clear();
                StartWave(map, waves[map.Curwave]);
                return;
            }
            else return;
        }

        public void StartWave(Spacemap map, Waves wave)
        {
            for(int i = 0; i < wave.NpcCount; i++)
            {
                if(wave.KeyNpc == 0)
                    map.InstanceNpcs.Add(new InstanceNpc(Randoms.CreateRandomID(), GameManager.GetShip(wave.NpcId), map,Position.GetPosOnCircle(Position.InvasionGatePosition, 4000), 0, wave.Multiplier, $" ~ {map.Curwave + 1}", false));
                if (wave.KeyNpc == 1)
                {
                    map.InstanceNpcs.Add(new InstanceNpc(Randoms.CreateRandomID(), GameManager.GetShip(wave.NpcId), map, Position.GetPosOnCircle(Position.InvasionGatePosition, 4000), 0, wave.Multiplier, $" ~ {map.Curwave + 1}", true));
                    for (int y = 0; y < wave.MinionsCount; y++)
                    {
                        map.InstanceNpcs[i].Minions.Add(new Escort(Randoms.CreateRandomID(), GameManager.GetShip(wave.MinionsID), map, map.InstanceNpcs[i].Position, wave.MinionsMultiplier, $" ~ {map.Curwave + 1}", map.InstanceNpcs[i]));
                        map.InstanceNpcs[i].Check();
                    }
                }
                //GameManager.SendChatSystemMessage($"Spawning wave {i} Invasion Gate Wave ID {wave.Id} NPC ID {wave.NpcId} Wave: {map.Curwave}");
            }
        }
    }
}
