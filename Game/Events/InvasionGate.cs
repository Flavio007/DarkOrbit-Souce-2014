using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ow.Game.Movements;
using Ow.Game.Objects;
using Ow.Managers;
using Ow.Managers.MySQLManager;
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
        public int MinionsMultiplier { get; set; }
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

        private readonly object stateLock = new object();
        private readonly Dictionary<int, Dictionary<int, int>> tierScores = new Dictionary<int, Dictionary<int, int>>();
        private readonly Dictionary<int, Dictionary<int, int>> tierHonorRemainders = new Dictionary<int, Dictionary<int, int>>();
        private readonly Dictionary<int, int> mapWaveQuarterProgress = new Dictionary<int, int>();

        private const float Portal1ForceMultiplier = 0.5f;
        private const float Portal2ForceMultiplier = 1.5f;
        private const float Portal3ForceMultiplier = 3.0f;

        private const float Portal1DropMultiplier = 0.8f;
        private const float Portal2DropMultiplier = 2.2f;
        private const float Portal3DropMultiplier = 5.0f;

        private void ResetTierState()
        {
            lock (stateLock)
            {
                tierScores.Clear();
                tierHonorRemainders.Clear();
                mapWaveQuarterProgress.Clear();

                for (var tier = 1; tier <= 3; tier++)
                {
                    tierScores[tier] = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } };
                    tierHonorRemainders[tier] = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } };
                }

                MmoScore = 0;
                EicScore = 0;
                VruScore = 0;
                CurrentWave = 1;
            }
        }

        private int GetPortalTierByMap(Spacemap map)
        {
            if (map == null) return 0;

            if (map.Id == SpacemapMMO1.Id || map.Id == SpacemapEIC1.Id || map.Id == SpacemapVRU1.Id)
                return 1;
            if (map.Id == SpacemapMMO2.Id || map.Id == SpacemapEIC2.Id || map.Id == SpacemapVRU2.Id)
                return 2;
            if (map.Id == SpacemapMMO3.Id || map.Id == SpacemapEIC3.Id || map.Id == SpacemapVRU3.Id)
                return 3;

            return 0;
        }

        private int GetPortalTierByLevel(int level)
        {
            if (level >= 5 && level <= 9) return 1;
            if (level >= 10 && level <= 14) return 2;
            if (level >= 15) return 3;
            return 0;
        }

        private float GetPortalForceMultiplier(Spacemap map)
        {
            var tier = GetPortalTierByMap(map);
            if (tier == 1) return Portal1ForceMultiplier;
            if (tier == 2) return Portal2ForceMultiplier;
            return Portal3ForceMultiplier;
        }

        private float GetPortalDropMultiplier(Spacemap map)
        {
            var tier = GetPortalTierByMap(map);
            if (tier == 1) return Portal1DropMultiplier;
            if (tier == 2) return Portal2DropMultiplier;
            return Portal3DropMultiplier;
        }

        private void ApplyDropMultiplier(Character npc, float dropMultiplier, float forceMultiplier)
        {
            if (npc == null || forceMultiplier <= 0) return;

            var dropRatio = dropMultiplier / forceMultiplier;
            npc.Credits = Convert.ToInt32(npc.Credits * dropRatio);
            npc.Experience = Convert.ToInt32(npc.Experience * dropRatio);
            npc.Honor = Convert.ToInt32(npc.Honor * dropRatio);
            npc.Uridium = Convert.ToInt32(npc.Uridium * dropRatio);
        }

        private Spacemap GetMapByFactionAndTier(int factionId, int tier)
        {
            switch (tier)
            {
                case 1:
                    if (factionId == 1) return SpacemapMMO1;
                    if (factionId == 2) return SpacemapEIC1;
                    if (factionId == 3) return SpacemapVRU1;
                    break;
                case 2:
                    if (factionId == 1) return SpacemapMMO2;
                    if (factionId == 2) return SpacemapEIC2;
                    if (factionId == 3) return SpacemapVRU2;
                    break;
                case 3:
                    if (factionId == 1) return SpacemapMMO3;
                    if (factionId == 2) return SpacemapEIC3;
                    if (factionId == 3) return SpacemapVRU3;
                    break;
            }

            return null;
        }

        private int GetCurrentWaveForFactionAndTier(int factionId, int tier)
        {
            var map = GetMapByFactionAndTier(factionId, tier);
            return map != null ? map.Curwave + 1 : 1;
        }

        public int GetCurrentWaveForFactionAndLevel(int factionId, int level)
        {
            var tier = GetPortalTierByLevel(level);
            if (tier <= 0) return 1;

            return GetCurrentWaveForFactionAndTier(factionId, tier);
        }

        public string GetSocketStatus(int factionId, int level)
        {
            var tier = GetPortalTierByLevel(level);
            Dictionary<int, int> scores;

            lock (stateLock)
            {
                if (tier <= 0 || !tierScores.ContainsKey(tier))
                    scores = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } };
                else
                    scores = new Dictionary<int, int>(tierScores[tier]);
            }

            var wave = GetCurrentWaveForFactionAndLevel(factionId, level);
            return $"{scores[1]}:{scores[2]}:{scores[3]}:{wave}";
        }

        private void SyncLegacyFieldsForTier(int tier)
        {
            if (!tierScores.ContainsKey(tier))
                return;

            MmoScore = tierScores[tier][1];
            EicScore = tierScores[tier][2];
            VruScore = tierScores[tier][3];
            CurrentWave = GetCurrentWaveForFactionAndTier(1, tier);
        }

        private void SendTierState(Player player, int tier)
        {
            if (player == null || tier <= 0) return;
            if (!tierScores.ContainsKey(tier)) return;

            Dictionary<int, int> scores;

            lock (stateLock)
            {
                scores = new Dictionary<int, int>(tierScores[tier]);
            }

            var wave = GetCurrentWaveForFactionAndTier(player.FactionId, tier);

            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.INIT_INVASION_SCOREBOARD}|{scores[1]}|{scores[2]}|{scores[3]}|{wave}");
            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_SCORE}|1|{scores[1]}");
            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_SCORE}|2|{scores[2]}");
            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_SCORE}|3|{scores[3]}");
            player.SendPacket($"0|n|{Ow.Net.netty.ServerCommands.SET_INVASION_WAVE}|{wave}");
        }

        private void BroadcastTierState(int tier)
        {
            foreach (var session in GameManager.GameSessions.Values)
            {
                var player = session.Player;
                if (player == null) continue;
                if (GetPortalTierByLevel(player.Level) != tier) continue;
                SendTierState(player, tier);
            }
        }

        public void AddHonorContribution(Player player, Spacemap map, int honorAmount)
        {
            if (!Started || player == null || map == null || honorAmount <= 0) return;

            var tier = GetPortalTierByMap(map);
            var factionId = player.FactionId;

            if (tier <= 0 || factionId < 1 || factionId > 3) return;

            lock (stateLock)
            {
                tierHonorRemainders[tier][factionId] += honorAmount;

                var points = tierHonorRemainders[tier][factionId] / 100;
                if (points > 0)
                {
                    tierScores[tier][factionId] += points;
                    tierHonorRemainders[tier][factionId] %= 100;
                    SyncLegacyFieldsForTier(tier);
                }
            }
        }

        public void SendWindowState(Player player)
        {
            if (player == null) return;

            var tier = GetPortalTierByLevel(player.Level);
            if (tier <= 0) return;

            SendTierState(player, tier);
        }

        public void Startup()
        {
            if (Started) return;
            Started = true;
            ResetTierState();

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
                mapWaveQuarterProgress[Maps[i].Id] = 0;
            }

            foreach (Portal gates in Portals)
                GameManager.SendCommandToMap(gates.Spacemap.Id, gates.GetAssetCreateCommand());

            var sql = SqlDatabaseManager.GetClient();
            for (int i = 1; i <= 22; i++)
            {
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
                    GameManager.SendPacketToMap(map.Id, $"0|A|STD|Level Invasion Gate Starting in {i} Seconds!");

                Thread.Sleep(1000);
            }

            foreach (Spacemap map in Maps)
                StartWave(map, waves[0]);

            Running();
        }

        public void Running()
        {
            while (true)
            {
                if (Started)
                {
                    foreach (Spacemap map in Maps)
                    {
                        int count = 0;
                        for (int i = 0; i < map.InstanceNpcs.Count; i++)
                        {
                            if (map.InstanceNpcs[i].Destroyed)
                                count++;
                        }

                        CheckWaveFinished(map, count, waves[map.Curwave]);
                    }
                }
                Thread.Sleep(5000);
            }
        }

        private void UpdateQuarterProgress(Spacemap map, int npcCount, Waves wave)
        {
            if (map == null || wave == null || wave.NpcCount <= 0) return;
            if (!mapWaveQuarterProgress.ContainsKey(map.Id)) mapWaveQuarterProgress[map.Id] = 0;

            var quarter = Math.Min(4, npcCount * 4 / wave.NpcCount);
            if (quarter <= mapWaveQuarterProgress[map.Id]) return;

            mapWaveQuarterProgress[map.Id] = quarter;
            var tier = GetPortalTierByMap(map);

            if (tier > 0)
                BroadcastTierState(tier);
        }

        public void CheckWaveFinished(Spacemap map, int npcCount, Waves wave)
        {
            UpdateQuarterProgress(map, npcCount, wave);

            if (npcCount < wave.NpcCount)
                return;

            var tier = GetPortalTierByMap(map);

            if (map.Curwave < waves.Count - 1)
                map.Curwave++;
            else
                map.Curwave = 0;

            if (map.FactionId >= 1 && map.FactionId <= 3)
                WavesCounter[map.FactionId - 1]++;

            map.InstanceNpcs.Clear();
            mapWaveQuarterProgress[map.Id] = 0;

            if (tier > 0)
            {
                lock (stateLock)
                    SyncLegacyFieldsForTier(tier);
                BroadcastTierState(tier);
            }

            StartWave(map, waves[map.Curwave]);
        }

        public void StartWave(Spacemap map, Waves wave)
        {
            var forceMultiplier = GetPortalForceMultiplier(map);
            var dropMultiplier = GetPortalDropMultiplier(map);

            for (int i = 0; i < wave.NpcCount; i++)
            {
                var currentWaveLabel = $" ~ {map.Curwave + 1}";

                if (wave.KeyNpc == 0)
                {
                    var npc = new InstanceNpc(
                        Randoms.CreateRandomID(),
                        GameManager.GetShip(wave.NpcId),
                        map,
                        Position.GetPosOnCircle(Position.InvasionGatePosition, 4000),
                        0,
                        wave.Multiplier * forceMultiplier,
                        currentWaveLabel,
                        false);

                    ApplyDropMultiplier(npc, dropMultiplier, forceMultiplier);
                    map.InstanceNpcs.Add(npc);
                }

                if (wave.KeyNpc == 1)
                {
                    var npc = new InstanceNpc(
                        Randoms.CreateRandomID(),
                        GameManager.GetShip(wave.NpcId),
                        map,
                        Position.GetPosOnCircle(Position.InvasionGatePosition, 4000),
                        0,
                        wave.Multiplier * forceMultiplier,
                        currentWaveLabel,
                        true);

                    ApplyDropMultiplier(npc, dropMultiplier, forceMultiplier);
                    map.InstanceNpcs.Add(npc);

                    for (int y = 0; y < wave.MinionsCount; y++)
                    {
                        var escort = new Escort(
                            Randoms.CreateRandomID(),
                            GameManager.GetShip(wave.MinionsID),
                            map,
                            npc.Position,
                            wave.MinionsMultiplier * forceMultiplier,
                            currentWaveLabel,
                            npc);

                        ApplyDropMultiplier(escort, dropMultiplier, forceMultiplier);
                        npc.Minions.Add(escort);
                        npc.Check();
                    }
                }
            }
        }
    }
}
