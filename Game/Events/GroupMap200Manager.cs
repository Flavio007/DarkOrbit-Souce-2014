using Ow.Game.Movements;
using Ow.Game.Objects;
using Ow.Game.Objects.Players;
using Ow.Game.Ticks;
using Ow.Managers;
using Ow.Net.netty;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ow.Game.Events
{
    class GroupMap200Manager : Tick
    {
        private sealed class LowNpcState
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public Position SpawnCenter { get; set; }
            public int KillThreshold { get; set; }
            public int ShipId { get; set; }
            public Queue<int> PendingBatches { get; } = new Queue<int>();
            public int AliveCount { get; set; }
            public int KilledSinceLastSpawn { get; set; }
        }

        private sealed class GroupMapInstance
        {
            public int Key { get; set; }
            public int MapId { get; set; }
            public Spacemap Spacemap { get; set; }
            public GroupMapExitPortal ExitPortal { get; set; }
            public GroupMapOreTradeStation OreTradeStation { get; set; }
            public List<GroupMapRelayStation> Relays { get; } = new List<GroupMapRelayStation>();
            public int CurrentRelayIndex { get; set; }
            public HashSet<string> TriggeredPois { get; } = new HashSet<string>();
            public Dictionary<string, LowNpcState> NpcStates { get; } = new Dictionary<string, LowNpcState>();
            public Dictionary<int, string> RuntimeNpcGroups { get; } = new Dictionary<int, string>();
            public bool CenturyFalconVagrantsSpawned { get; set; }
            public bool FinalTriggerActivated { get; set; }
            public bool FalconDestroyed { get; set; }
            public bool ShutdownScheduled { get; set; }
            public DateTime ShutdownAt { get; set; }
            public bool ShutdownInProgress { get; set; }
        }

        public const int VisualMapId = 200;
        private const int EntryPortalGraphicId = 34;
        private const int DynamicMapStartId = 20500;
        private const int FutureMinimumGroupMembers = 3;
        private const int FutureWaitingTimeSeconds = 294;
        private const int FalconShutdownDelaySeconds = 60;
        private const int CenturyFalconNpcId = 90;
        private const int CorsairNpcId = 91;
        private const int OutcastNpcId = 92;
        private const int MarauderNpcId = 93;
        private const int VagrantNpcId = 94;
        private const int ConvictNpcId = 95;
        private const int HooliganNpcId = 96;
        private const int RavagerNpcId = 97;
        private const string VagrantGroupKey = "vagrant";
        private const string OutcastGroupKey = "outcast";
        private const string CorsairGroupKey = "corsair";
        private const string MarauderGroupKey = "marauder";
        private const string HooliganGroupKey = "hooligan";
        private const string ConvictGroupKey = "convict";
        private const string RavagerGroupKey = "ravager";
        private const string CenturyFalconGroupKey = "century_falcon";
        private const string FalconVagrantGroupKey = "falcon_vagrants";

        private static readonly Position BasePortalPosition = new Position(Position.InvasionGatePosition.X, Position.InvasionGatePosition.Y);
        private static readonly Position InstanceSpawnPosition = new Position(1000, 12000);
        private static readonly Position ExitPortalPosition = new Position(1700, 12000);
        private static readonly Position OreTradePosition = new Position(10000, 5500);
        private static readonly Position Wave1SpawnPosition = new Position(2400, 10000);
        private static readonly Position Wave2SpawnPosition = new Position(2500, 5750);
        private static readonly Position Wave3SpawnPosition = new Position(5200, 3500);
        private static readonly Position Wave4SpawnPosition = new Position(11500, 5500);
        private static readonly Position Relay2SpawnPosition = new Position(7600, 11200);
        private static readonly Position Relay3SpawnPosition = new Position(16750, 11200);
        private static readonly Position Relay4SpawnPosition = new Position(17100, 4500);
        private static readonly Position Wave5SpawnPosition = new Position(12000, 9000);
        private static readonly Position Wave7SpawnPosition = new Position(15250, 8050);
        private static readonly Position Wave9SpawnPosition = new Position(15500, 2200);
        private static readonly Position CenturyFalconSpawnPosition = new Position(11200, 6000);
        private static readonly Position[] RelayPositions =
        {
            new Position(2750, 1750), // first relay (upper left)
            new Position(6000, 11750), // second relay (lower left)
            new Position(18250, 11750), // third relay (lower right)
            new Position(18250, 4200) // final relay (upper right)
        };

        private readonly object instanceLock = new object();
        private readonly Dictionary<int, GroupMapInstance> instancesByKey = new Dictionary<int, GroupMapInstance>();
        private readonly Dictionary<int, GroupMapInstance> instancesByMap = new Dictionary<int, GroupMapInstance>();
        private int nextDynamicMapId = DynamicMapStartId;
        private bool initialized;

        public void Initialize()
        {
            lock (instanceLock)
            {
                if (initialized)
                    return;

                SpawnEntryPortal(1, 1);
                SpawnEntryPortal(2, 5);
                SpawnEntryPortal(3, 9);
                Program.TickManager.AddTick(this);
                initialized = true;
            }
        }

        public void Tick()
        {
            List<GroupMapInstance> snapshot;
            lock (instanceLock)
                snapshot = instancesByMap.Values.ToList();

            foreach (var instance in snapshot)
                ProcessInstance(instance);
        }

        public void PreparePlayerForLogin(Player player)
        {
            if (player == null || !ShouldUseGroupMap(player))
                return;

            GroupMapInstance instance;
            lock (instanceLock)
                instance = GetOrCreateInstance(player);

            if (instance?.Spacemap == null)
                return;

            player.Spacemap = instance.Spacemap;
            player.SetPosition(ResolveLoginPosition(player));

            if (player.LastPosition != null)
            {
                player.LastPosition.map = VisualMapId;
                player.LastPosition.x = player.Position.X;
                player.LastPosition.y = player.Position.Y;
            }
        }

        public async void Enter(Player player, int sourcePortalId)
        {
            if (player == null || player.Storage.Jumping)
                return;

            GroupMapInstance instance;
            lock (instanceLock)
                instance = GetOrCreateInstance(player);

            if (instance == null)
                return;

            SendFutureGroupRequirementMessage(player);

            await JumpPlayer(player, instance.MapId, new Position(InstanceSpawnPosition.X, InstanceSpawnPosition.Y), sourcePortalId, VisualMapId);
        }

        public async void ExitToBase(Player player, int sourcePortalId)
        {
            if (player == null || player.Storage.Jumping)
                return;

            GroupMapInstance instance = null;
            lock (instanceLock)
                instancesByMap.TryGetValue(player.Spacemap != null ? player.Spacemap.Id : 0, out instance);

            await JumpPlayer(player, player.GetBaseMapId(), player.GetBasePosition(), sourcePortalId, player.GetBaseMapId());

            if (instance != null)
                CleanupIfEmpty(instance);
        }

        private void SpawnEntryPortal(int factionId, int mapId)
        {
            var spacemap = GameManager.GetSpacemap(mapId);
            if (spacemap == null)
                return;

            var portal = new GroupMapEntryPortal(this, spacemap, new Position(BasePortalPosition.X, BasePortalPosition.Y), factionId);
            GameManager.SendCommandToMap(spacemap.Id, portal.GetAssetCreateCommand());
        }

        private GroupMapInstance GetOrCreateInstance(Player player)
        {
            var instanceKey = GetInstanceKey(player);
            if (instancesByKey.TryGetValue(instanceKey, out var instance) && instance.Spacemap != null)
                return instance;

            var options = new OptionsBase
            {
                StarterMap = false,
                PvpMap = false,
                RangeDisabled = true,
                CloakBlocked = true,
                LogoutBlocked = false,
                DeathLocationRepair = false
            };

            var mapId = AllocateDynamicMapId();
            var map = new Spacemap(
                mapId,
                $"Low-{Math.Abs(instanceKey)}",
                0,
                null,
                null,
                null,
                options,
                null,
                VisualMapId,
                true);

            map.Instance = true;
            AddPois(map);
            instance = new GroupMapInstance
            {
                Key = instanceKey,
                MapId = map.Id,
                Spacemap = map,
                ExitPortal = new GroupMapExitPortal(this, map, new Position(ExitPortalPosition.X, ExitPortalPosition.Y), EntryPortalGraphicId)
            };

            AddAssets(instance);
            GameManager.Spacemaps.TryAdd(map.Id, map);

            instancesByKey[instanceKey] = instance;
            instancesByMap[map.Id] = instance;
            return instance;
        }

        private void AddPois(Spacemap map)
        {
            foreach (var poi in CreatePois())
                map.POIs[poi.Id] = poi;
        }

        private void AddAssets(GroupMapInstance instance)
        {
            if (instance?.Spacemap == null)
                return;

            for (var index = 0; index < RelayPositions.Length; index++)
            {
                var relayPosition = RelayPositions[index];
                var relay = new GroupMapRelayStation(this, instance.Spacemap, new Position(relayPosition.X, relayPosition.Y), index);
                if (index > 0)
                    relay.Hide();

                instance.Relays.Add(relay);
            }

            instance.OreTradeStation = new GroupMapOreTradeStation(instance.Spacemap, new Position(OreTradePosition.X, OreTradePosition.Y));
            RevealRelay(instance, 0, true);
        }

        private void ProcessInstance(GroupMapInstance instance)
        {
            if (instance?.Spacemap == null)
                return;

            TryStartPoiWave(instance, "Wave1Trigger", VagrantGroupKey, "Vagrant", Wave1SpawnPosition, 10, new[] { 12, 12, 13 }, VagrantNpcId, 0);
            TryStartPoiWave(instance, "Wave2Trigger", OutcastGroupKey, "Outcast", Wave2SpawnPosition, 10, new[] { 10, 12, 12, 12 }, OutcastNpcId, 0);
            TryStartPoiWave(instance, "Wave3Trigger", CorsairGroupKey, "Corsair", Wave3SpawnPosition, 8, new[] { 10, 10, 10 }, CorsairNpcId, 0);
            TryStartPoiWave(instance, "Wave4Trigger", MarauderGroupKey, "Marauder", Wave4SpawnPosition, 10, new[] { 13, 13, 12, 13 }, MarauderNpcId, 0);
            TryStartPoiWave(instance, "Wave5Trigger", HooliganGroupKey, "Hooligan", Wave5SpawnPosition, 8, new[] { 10, 10, 10, 10, 9 }, HooliganNpcId, 2);
            TryStartPoiWave(instance, "Wave7Trigger", ConvictGroupKey, "Convict", Wave7SpawnPosition, 6, new[] { 7, 7, 6 }, ConvictNpcId, 3);
            TryStartPoiWave(instance, "Wave9Trigger", RavagerGroupKey, "Ravager", Wave9SpawnPosition, 10, new[] { 13, 13, 13 }, RavagerNpcId, 3);
            TryTriggerCenturyFalconSupport(instance);
            instance.OreTradeStation?.ProcessRepairAura();
            ProcessScheduledShutdown(instance);
        }

        private void TryStartPoiWave(GroupMapInstance instance, string poiId, string groupKey, string displayName, Position spawnCenter, int killThreshold, int[] batches, int shipId, int requiredRelayCount)
        {
            if (instance == null || string.IsNullOrEmpty(poiId) || string.IsNullOrEmpty(groupKey))
                return;

            lock (instanceLock)
            {
                if (instance.TriggeredPois.Contains(poiId) || instance.NpcStates.ContainsKey(groupKey))
                    return;
            }

            if (!HasRequiredActiveRelays(instance, requiredRelayCount))
                return;

            if (!AnyPlayerInPoi(instance, poiId))
                return;

            lock (instanceLock)
            {
                if (instance.TriggeredPois.Contains(poiId) || instance.NpcStates.ContainsKey(groupKey))
                    return;

                instance.TriggeredPois.Add(poiId);
                AddBatchesAndSpawn(instance, groupKey, displayName, spawnCenter, killThreshold, shipId, batches, true, $"LoW - {displayName}s detected.");
            }
        }

        private bool HasRequiredActiveRelays(GroupMapInstance instance, int requiredRelayCount)
        {
            return instance != null && instance.CurrentRelayIndex >= requiredRelayCount;
        }

        private void StartFinalEncounter(GroupMapInstance instance)
        {
            if (instance == null)
                return;

            if (instance.FinalTriggerActivated)
                return;

            instance.FinalTriggerActivated = true;
            instance.OreTradeStation?.SetRepairAuraActive(true);
            AddBatchesAndSpawn(instance, ConvictGroupKey, "Convict", Wave9SpawnPosition, 6, ConvictNpcId, new[] { 7, 7 }, true, "LoW - Final assault detected.");
            AddBatchesAndSpawn(instance, CenturyFalconGroupKey, "Century Falcon", CenturyFalconSpawnPosition, 9999, CenturyFalconNpcId, new[] { 1 }, true, "LoW - Century Falcon deployed.");
        }

        private void ProcessScheduledShutdown(GroupMapInstance instance)
        {
            if (instance == null || !instance.ShutdownScheduled || instance.ShutdownInProgress || instance.ShutdownAt > DateTime.Now)
                return;

            instance.ShutdownInProgress = true;
            _ = CloseInstanceToBase(instance);
        }

        private void ScheduleShutdown(GroupMapInstance instance)
        {
            if (instance == null || instance.ShutdownScheduled)
                return;

            instance.FalconDestroyed = true;
            instance.ShutdownScheduled = true;
            instance.ShutdownAt = DateTime.Now.AddSeconds(FalconShutdownDelaySeconds);
            GameManager.SendPacketToMap(instance.MapId, $"0|A|STD|LoW - Century Falcon destroyed. The map will close in {FalconShutdownDelaySeconds} seconds.");
        }

        private async Task CloseInstanceToBase(GroupMapInstance instance)
        {
            if (instance?.Spacemap == null)
                return;

            var players = instance.Spacemap.Characters.Values
                .OfType<Player>()
                .Where(player => player != null && !player.Destroyed)
                .ToList();

            foreach (var player in players)
                await JumpPlayer(player, player.GetBaseMapId(), player.GetBasePosition(), instance.ExitPortal?.Id ?? 0, VisualMapId);

            CleanupIfEmpty(instance);
        }

        private void TryTriggerCenturyFalconSupport(GroupMapInstance instance)
        {
            if (instance == null || instance.Spacemap == null)
                return;

            lock (instanceLock)
            {
                if (instance.CenturyFalconVagrantsSpawned)
                    return;

                if (!instance.NpcStates.TryGetValue(CenturyFalconGroupKey, out var falconState) || falconState.AliveCount <= 0)
                    return;
            }

            var falconNpc = GetAliveGroupNpc(instance, CenturyFalconGroupKey);
            if (falconNpc == null)
                return;

            var shouldSpawn = instance.Spacemap.Characters.Values
                .OfType<Player>()
                .Any(player => player != null && !player.Destroyed && player.CurrentHitPoints > 0 && player.Position.DistanceTo(falconNpc.Position) <= 700);

            if (!shouldSpawn)
                return;

            lock (instanceLock)
            {
                if (instance.CenturyFalconVagrantsSpawned)
                    return;

                instance.CenturyFalconVagrantsSpawned = true;
                AddBatchesAndSpawn(instance, FalconVagrantGroupKey, "Vagrant", falconNpc.Position, 9999, VagrantNpcId, new[] { 30 }, true, "LoW - Century Falcon deployed reinforcements.");
            }
        }

        private InstanceNpc GetAliveGroupNpc(GroupMapInstance instance, string groupKey)
        {
            if (instance?.Spacemap == null || string.IsNullOrEmpty(groupKey))
                return null;

            lock (instance.Spacemap.InstanceNpcs)
            {
                return instance.Spacemap.InstanceNpcs
                    .FirstOrDefault(npc => npc != null && instance.RuntimeNpcGroups.TryGetValue(npc.Id, out var runtimeGroup) && runtimeGroup == groupKey && !npc.Destroyed);
            }
        }

        private void AddBatchesAndSpawn(GroupMapInstance instance, string groupKey, string displayName, Position spawnCenter, int killThreshold, int shipId, int[] batches, bool spawnNow, string announcement)
        {
            if (instance == null || instance.Spacemap == null || batches == null || batches.Length == 0)
                return;

            if (!instance.NpcStates.TryGetValue(groupKey, out var state))
            {
                state = new LowNpcState
                {
                    Key = groupKey,
                    DisplayName = displayName,
                    SpawnCenter = new Position(spawnCenter.X, spawnCenter.Y),
                    KillThreshold = killThreshold,
                    ShipId = shipId
                };

                instance.NpcStates[groupKey] = state;
            }
            else
            {
                state.SpawnCenter = new Position(spawnCenter.X, spawnCenter.Y);
                state.KillThreshold = killThreshold;
                state.ShipId = shipId;
            }

            foreach (var batch in batches)
            {
                if (batch > 0)
                    state.PendingBatches.Enqueue(batch);
            }

            if (!string.IsNullOrWhiteSpace(announcement))
                GameManager.SendPacketToMap(instance.MapId, $"0|A|STD|{announcement}");

            if (spawnNow)
                SpawnNextBatch(instance, state);
        }

        private void SpawnNextBatch(GroupMapInstance instance, LowNpcState state)
        {
            if (instance == null || state == null || state.PendingBatches.Count <= 0)
                return;

            var ship = ResolveLowShip(state.ShipId);
            if (ship == null)
                return;

            var batchSize = state.PendingBatches.Dequeue();
            state.KilledSinceLastSpawn = 0;

            SendMapPing(instance.MapId, state.SpawnCenter);

            for (var index = 0; index < batchSize; index++)
            {
                var position = Position.GetPosOnCircle(state.SpawnCenter, 900 + (index % 4) * 150);
                var npc = new InstanceNpc(
                    Randoms.CreateRandomID(),
                    ship,
                    instance.Spacemap,
                    position,
                    0,
                    1,
                    " ~ LoW",
                    ship.Id == Ship.CENTURY_FALCON);

                npc.UseMapWideChaseRange = false;

                lock (instance.Spacemap.InstanceNpcs)
                    instance.Spacemap.InstanceNpcs.Add(npc);

                instance.RuntimeNpcGroups[npc.Id] = state.Key;
                state.AliveCount++;
            }
        }

        private void RevealRelay(GroupMapInstance instance, int relayIndex, bool ping)
        {
            if (instance == null || relayIndex < 0 || relayIndex >= instance.Relays.Count)
                return;

            var relay = instance.Relays[relayIndex];
            if (relay == null)
                return;

            relay.Show();

            if (ping)
                SendMapPing(instance.MapId, relay.Position);
        }

        private void SendMapPing(int mapId, Position position)
        {
            if (position == null)
                return;

            GameManager.SendCommandToMap(mapId, GroupPingCommand.write(position.X, position.Y));
        }

        private Ship ResolveLowShip(int shipId)
        {
            var ship = GameManager.GetShip(shipId);
            if (ship == null)
                Logger.Log("error_log", $"- [GroupMap200Manager.cs] Unable to resolve LoW ship id '{shipId}'.");

            return ship;
        }

        private bool AnyPlayerInPoi(GroupMapInstance instance, string poiId)
        {
            if (instance?.Spacemap == null || string.IsNullOrEmpty(poiId))
                return false;

            if (!instance.Spacemap.POIs.TryGetValue(poiId, out var poi) || poi == null)
                return false;

            return instance.Spacemap.Characters.Values
                .OfType<Player>()
                .Any(player => player != null && !player.Destroyed && player.CurrentHitPoints > 0 && IsInsidePoi(poi, player.Position));
        }

        private bool IsInsidePoi(POI poi, Position position)
        {
            if (poi == null || position == null || poi.ShapeCords == null || poi.ShapeCords.Count == 0)
                return false;

            if (poi.Shape == POIShapes.CIRCLE && poi.ShapeCords.Count > 1)
                return position.DistanceTo(poi.ShapeCords[0]) <= poi.ShapeCords[1].X;

            var minX = poi.ShapeCords.Min(point => point.X);
            var maxX = poi.ShapeCords.Max(point => point.X);
            var minY = poi.ShapeCords.Min(point => point.Y);
            var maxY = poi.ShapeCords.Max(point => point.Y);
            return position.X >= minX && position.X <= maxX && position.Y >= minY && position.Y <= maxY;
        }

        private List<POI> CreatePois()
        {
            return new List<POI>
            {
                new POI("Wave4Trigger", 2, 0, 2, CreatePositions(10500, 4000, 12500, 4000, 12500, 7000, 10500, 7000)),
                new POI("Wave3Trigger", 2, 0, 2, CreatePositions(4400, 4500, 6000, 4500, 6000, 2500, 4400, 2500)),
                new POI("Wave5Trigger", 2, 0, 2, CreatePositions(10500, 9500, 13500, 9500, 13500, 8300, 10500, 8300)),
                new POI("Wave7Trigger", 2, 0, 2, CreatePositions(14300, 6900, 16200, 6900, 16200, 9200, 14300, 9200)),
                new POI("Wave9Trigger", 2, 0, 2, CreatePositions(14500, -20000, 14500, 3000, 16500, 3000, 16500, -20000)),
                new POI("Wave2Trigger", 2, 0, 2, CreatePositions(-20000, 6500, 4000, 6500, 4000, 5000, -20000, 5000)),
                new POI("Block2", 5, 1, 2, CreatePositions(4608, 9984, 11008, 9984, 11008, 8448, 4608, 8448)),
                new POI("Block1", 5, 1, 2, CreatePositions(3072, 43008, 3072, 4096, 4608, 4096, 4608, 43008)),
                new POI("Block4", 5, 1, 2, CreatePositions(16128, 5376, 50944, 5376, 50944, 6912, 16128, 6912)),
                new POI("Block3", 5, 1, 2, CreatePositions(13056, 8960, 14592, 8960, 14592, 43008, 13056, 43008)),
                new POI("Wave1Trigger", 2, 0, 2, CreatePositions(-20000, 9500, 3500, 9500, 3500, 10500, -20000, 10500)),
                new POI("Block6", 5, 1, 2, CreatePositions(5120, -29952, 5120, 2560, 11008, 2560, 11008, -29952)),
                new POI("Block5", 5, 1, 2, CreatePositions(14592, 5888, 16128, 5888, 16128, 2560, 14592, 2560)),
                new POI("Relay1VideoPOI", 2, 0, 0, CreatePositions(2750, 1750, 500)),
                new POI("HQVideoPOI", 2, 0, 0, CreatePositions(10000, 5500, 750)),
                new POI("LoW Cage Zone", 5, 6, 2, CreatePositions(0, 0, 0, 13500, 21000, 13500, 21000, 0), true, true),
                new POI("Equippable Zone", 16, 0, 0, CreatePositions(10000, 5500, 300)),
                new POI("mapAssetRangeZone-150000145", 7, 0, 0, CreatePositions(10000, 5500, 300))
            };
        }

        private static List<Position> CreatePositions(params int[] coordinates)
        {
            var positions = new List<Position>();
            for (var i = 0; i + 1 < coordinates.Length; i += 2)
                positions.Add(new Position(coordinates[i], coordinates[i + 1]));

            return positions;
        }

        private async Task JumpPlayer(Player player, int targetMapId, Position targetPosition, int sourcePortalId, int clientMapId)
        {
            if (player == null || player.Storage.Jumping)
                return;

            var destination = targetPosition ?? player.GetBasePosition();
            player.Storage.Jumping = true;

            try
            {
                player.SendCommand(ActivatePortalCommand.write(clientMapId, sourcePortalId));
                await Task.Delay(Portal.JUMP_DELAY);

                var targetMap = GameManager.GetSpacemap(targetMapId) ?? GameManager.GetSpacemap(player.GetBaseMapId());
                if (targetMap == null)
                    return;

                if (targetMap.Id == player.GetBaseMapId())
                    destination = player.GetBasePosition();

                player.LastCombatTime = DateTime.Now.AddSeconds(-999);
                player.Spacemap?.RemoveCharacter(player);
                player.CurrentInRangePortalId = -1;
                player.Deselection();
                player.Storage.InRangeAssets.Clear();
                player.Storage.InRangeObjects.Clear();
                player.InRangeCharacters.Clear();
                player.SetPosition(destination);
                player.Spacemap = targetMap;

                if (player.LastPosition != null)
                {
                    player.LastPosition.map = targetMap.PersistAsVisualMapId ? targetMap.VisualMapId : targetMap.Id;
                    player.LastPosition.x = destination.X;
                    player.LastPosition.y = destination.Y;
                }

                player.Spacemap.AddAndInitPlayer(player);
                EnforceLowMapRestrictions(player, targetMap);
                targetMap.CheckActivatables(player);
            }
            finally
            {
                player.Storage.Jumping = false;
            }
        }

        private void EnforceLowMapRestrictions(Player player, Spacemap targetMap)
        {
            if (player == null || targetMap == null || !targetMap.Options.CloakBlocked)
                return;

            if (player.Invisible)
            {
                player.Invisible = false;
                player.UpdateShipStatus();
                var cloakPacket = $"0|n|INV|{player.Id}|0";
                player.SendPacket(cloakPacket);
                player.SendPacketToInRangePlayers(cloakPacket);
                player.SettingsManager?.SendNewItemStatus(Objects.Players.Managers.CpuManager.CLK_XL);
            }

            if (player.Pet != null && player.Pet.Activated && player.Pet.Invisible)
            {
                player.Pet.Invisible = false;
                player.Pet.SendPacketToInRangePlayers($"0|n|INV|{player.Pet.Id}|0");
            }
        }

        private void CleanupIfEmpty(GroupMapInstance instance)
        {
            if (instance?.Spacemap == null)
                return;

            lock (instanceLock)
            {
                if (instance.Spacemap.Characters.Values.OfType<Player>().Any())
                    return;

                CleanupInstance(instance);
            }
        }

        private void CleanupInstance(GroupMapInstance instance)
        {
            if (instance == null)
                return;

            instance.ExitPortal?.Remove();
            instance.ExitPortal = null;
            instance.OreTradeStation?.SetRepairAuraActive(false);
            instance.OreTradeStation = null;

            if (instance.Spacemap != null)
            {
                foreach (var character in instance.Spacemap.Characters.Values.ToList())
                {
                    Program.TickManager.RemoveTick(character);
                    character.Destroyed = true;
                    instance.Spacemap.RemoveCharacter(character);
                }

                Program.TickManager.RemoveTick(instance.Spacemap);
                GameManager.Spacemaps.TryRemove(instance.Spacemap.Id, out _);
            }

            instancesByKey.Remove(instance.Key);
            instancesByMap.Remove(instance.MapId);
        }

        public void HandleNpcDestroyed(InstanceNpc npc)
        {
            if (npc?.Spacemap == null)
                return;

            lock (instanceLock)
            {
                if (!instancesByMap.TryGetValue(npc.Spacemap.Id, out var instance) || instance == null)
                    return;

                if (!instance.RuntimeNpcGroups.TryGetValue(npc.Id, out var groupKey))
                    return;

                instance.RuntimeNpcGroups.Remove(npc.Id);

                if (!instance.NpcStates.TryGetValue(groupKey, out var state) || state == null)
                    return;

                state.AliveCount = Math.Max(0, state.AliveCount - 1);
                state.KilledSinceLastSpawn++;

                if (groupKey == CenturyFalconGroupKey && state.AliveCount <= 0)
                    ScheduleShutdown(instance);

                if (state.PendingBatches.Count > 0 && state.KilledSinceLastSpawn >= state.KillThreshold)
                    SpawnNextBatch(instance, state);
            }
        }

        private Position ResolveLoginPosition(Player player)
        {
            if (player?.LastPosition != null && player.LastPosition.map == VisualMapId)
                return new Position(player.LastPosition.x, player.LastPosition.y);

            return new Position(InstanceSpawnPosition.X, InstanceSpawnPosition.Y);
        }

        private bool ShouldUseGroupMap(Player player)
        {
            if (player == null)
                return false;

            if (player.LastPosition != null && player.LastPosition.map == VisualMapId)
                return true;

            return player.Spacemap != null && player.Spacemap.VisualMapId == VisualMapId;
        }

        private int GetInstanceKey(Player player)
        {
            if (player?.Group != null)
            {
                if (player.Group.GroupMapInstanceKey <= 0)
                    player.Group.GroupMapInstanceKey = Randoms.CreateRandomID();

                return -Math.Abs(player.Group.GroupMapInstanceKey);
            }

            return player != null ? player.Id : 0;
        }

        private int AllocateDynamicMapId()
        {
            while (GameManager.GetSpacemap(nextDynamicMapId) != null)
                nextDynamicMapId++;

            return nextDynamicMapId++;
        }

        public int TryChargeRelay(GroupMapRelayStation relay, Player player, int amount)
        {
            if (relay == null || player == null || amount <= 0 || relay.Spacemap == null)
                return 0;

            lock (instanceLock)
            {
                if (!instancesByMap.TryGetValue(relay.Spacemap.Id, out var instance) || instance == null)
                    return 0;

                if (player.Spacemap == null || player.Spacemap.Id != instance.MapId)
                    return 0;

                if (instance.CurrentRelayIndex >= instance.Relays.Count)
                    return 0;

                var expectedRelay = instance.Relays[instance.CurrentRelayIndex];
                if (expectedRelay == null || expectedRelay.Id != relay.Id)
                    return 0;

                var chargeAmount = relay.Charge(amount);
                if (relay.CurrentHitPoints >= relay.MaxHitPoints)
                {
                    instance.CurrentRelayIndex++;
                    HandleRelayActivated(instance, relay.ActivationOrder + 1);
                }

                return chargeAmount;
            }
        }

        private void HandleRelayActivated(GroupMapInstance instance, int activatedRelayCount)
        {
            if (instance == null)
                return;

            RevealRelay(instance, activatedRelayCount, true);

            switch (activatedRelayCount)
            {
                case 1:
                    GameManager.SendPacketToMap(instance.MapId, "0|A|STD|LoW - First beacon activated.");
                    break;
                case 2:
                    GameManager.SendPacketToMap(instance.MapId, "0|A|STD|LoW - Second beacon activated.");
                    break;
                case 3:
                    GameManager.SendPacketToMap(instance.MapId, "0|A|STD|LoW - Third beacon activated.");
                    break;
                case 4:
                    GameManager.SendPacketToMap(instance.MapId, "0|A|STD|LoW - Final beacon activated.");
                    StartFinalEncounter(instance);
                    break;
            }
        }

        private void SendFutureGroupRequirementMessage(Player player)
        {
            if (player == null)
                return;

            var currentMemberCount = player.Group?.Members?.Count ?? 1;
            var missingMemberCount = Math.Max(0, FutureMinimumGroupMembers - currentMemberCount);
            if (missingMemberCount <= 0)
                return;

            player.SendPacket($"0|n|MSG|5|0|msg_groupgate_waiting_for_group_members|{{w:%MISSINGMEMBERCOUNT%,v:{missingMemberCount}}},{{w:%WAITINGTIME%,v:{FutureWaitingTimeSeconds}}}");
        }
    }

    class GroupMapEntryPortal : Portal
    {
        private readonly GroupMap200Manager manager;
        private readonly int factionId;

        public GroupMapEntryPortal(GroupMap200Manager manager, Spacemap spacemap, Position position, int factionId)
            : base(spacemap, position, position, GroupMap200Manager.VisualMapId, 34, factionId, true, true, false)
        {
            this.manager = manager;
            this.factionId = factionId;
        }

        public override bool IsVisibleTo(Player player)
        {
            return player != null && player.FactionId == factionId;
        }

        public override bool CanInteract(Player player)
        {
            return IsVisibleTo(player);
        }

        public override void Click(GameSession gameSession)
        {
            if (gameSession?.Player == null)
                return;

            manager.Enter(gameSession.Player, Id);
        }
    }

    class GroupMapExitPortal : Portal
    {
        private readonly GroupMap200Manager manager;

        public GroupMapExitPortal(GroupMap200Manager manager, Spacemap spacemap, Position position, int graphicsId)
            : base(spacemap, position, position, 0, graphicsId, 0, true, true, false)
        {
            this.manager = manager;
        }

        public override bool IsVisibleTo(Player player)
        {
            return player != null && player.Spacemap != null && player.Spacemap.Id == Spacemap.Id;
        }

        public override bool CanInteract(Player player)
        {
            return IsVisibleTo(player);
        }

        public override void Click(GameSession gameSession)
        {
            if (gameSession?.Player == null)
                return;

            manager.ExitToBase(gameSession.Player, Id);
        }
    }

    class GroupMapRelayStation : Activatable
    {
        public const int RelayAssetId = 100000101;
        public const int MaxCharge = 100;
        private readonly GroupMap200Manager manager;

        public override int MinimumHitpoints => MaxCharge;
        public int ActivationOrder { get; }
        public bool VisibleOnMap { get; private set; } = true;

        public override string Name { get; set; }
        public override Clan Clan { get; set; }
        public override Position Position { get; set; }
        public override Spacemap Spacemap { get; set; }
        public override int FactionId { get; set; }
        public override int CurrentHitPoints { get; set; }
        public override int MaxHitPoints { get; set; }
        public override int CurrentNanoHull { get; set; }
        public override int MaxNanoHull { get; set; }
        public override int CurrentShieldPoints { get; set; }
        public override int MaxShieldPoints { get; set; }
        public override double ShieldAbsorption { get; set; }
        public override double ShieldPenetration { get; set; }
        public bool IsFullyCharged => CurrentHitPoints >= MaxHitPoints;

        public GroupMapRelayStation(GroupMap200Manager manager, Spacemap spacemap, Position position, int activationOrder)
            : base(RelayAssetId + activationOrder, spacemap, 0, position, GameManager.GetClan(0), AssetTypeModule.RELAY_STATION)
        {
            this.manager = manager;
            ActivationOrder = activationOrder;
            Name = $"Relay {101 + activationOrder}";
            Clan = GameManager.GetClan(0);
            Spacemap = spacemap;
            Position = position;
            FactionId = 0;
            MaxHitPoints = MaxCharge;
            CurrentHitPoints = 0;
            CurrentNanoHull = 0;
            MaxNanoHull = 0;
            CurrentShieldPoints = 0;
            MaxShieldPoints = 0;
            ShieldAbsorption = 0;
            ShieldPenetration = 0;
        }

        public override void Tick()
        {
        }

        public override void Click(GameSession gameSession)
        {
        }

        public int Charge(int amount)
        {
            if (amount <= 0)
                return 0;

            var previous = CurrentHitPoints;
            CurrentHitPoints = Math.Min(MaxHitPoints, CurrentHitPoints + amount);
            LastCombatTime = DateTime.Now;
            return CurrentHitPoints - previous;
        }

        public int TryCharge(Player player, int amount)
        {
            return manager?.TryChargeRelay(this, player, amount) ?? 0;
        }

        public void Show()
        {
            if (VisibleOnMap || Spacemap == null)
                return;

            Spacemap.Activatables[Id] = this;
            VisibleOnMap = true;
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
        }

        public void Hide()
        {
            if (!VisibleOnMap || Spacemap == null)
                return;

            Spacemap.Activatables.TryRemove(Id, out var removedRelay);
            VisibleOnMap = false;
            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(GetAssetType(), Id));
        }

        public override int GetVisualDesignId()
        {
            return 1;
        }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            return AssetCreateCommand.write(GetAssetType(), Name,
                0, "", Id, GetVisualDesignId(), 0,
                Position.X, Position.Y, 0, false, false, true, false,
                new ClanRelationModule(clanRelationModule),
                new List<VisualModifierCommand>());
        }
    }

    class GroupMapOreTradeStation : Activatable
    {
        public const int OreTradeAssetId = 150000145;
        private const short NeutralOreTradeAssetType = 52;
        private const int RepairAuraAmount = 5000;
        private const int RepairAuraIntervalSeconds = 10;
        private const int RepairAuraRange = 700;

        private readonly Dictionary<int, Player> activeRepairTargets = new Dictionary<int, Player>();
        private DateTime lastRepairTick = DateTime.MinValue;

        public override string Name { get; set; }
        public override Clan Clan { get; set; }
        public override Position Position { get; set; }
        public override Spacemap Spacemap { get; set; }
        public override int FactionId { get; set; }
        public override int CurrentHitPoints { get; set; }
        public override int MaxHitPoints { get; set; }
        public override int CurrentNanoHull { get; set; }
        public override int MaxNanoHull { get; set; }
        public override int CurrentShieldPoints { get; set; }
        public override int MaxShieldPoints { get; set; }
        public override double ShieldAbsorption { get; set; }
        public override double ShieldPenetration { get; set; }

        public GroupMapOreTradeStation(Spacemap spacemap, Position position)
            : base(OreTradeAssetId, spacemap, 0, position, GameManager.GetClan(0), NeutralOreTradeAssetType)
        {
            Name = "OreTrade";
            Clan = GameManager.GetClan(0);
            Spacemap = spacemap;
            Position = position;
            FactionId = 0;
            CurrentHitPoints = 0;
            MaxHitPoints = 0;
            CurrentNanoHull = 0;
            MaxNanoHull = 0;
            CurrentShieldPoints = 0;
            MaxShieldPoints = 0;
            ShieldAbsorption = 0;
            ShieldPenetration = 0;
            ShowBubble = true;
        }

        public bool ShowBubble { get; }
        public bool RepairAuraActive { get; private set; }

        public override void Tick()
        {
        }

        public void SetRepairAuraActive(bool active)
        {
            if (RepairAuraActive == active)
                return;

            RepairAuraActive = active;

            if (RepairAuraActive)
                AddVisualModifier(VisualModifierCommand.EMERGENCY_REPAIR_EFFECT, 0, "", 0, true);
            else
            {
                RemoveVisualModifier(VisualModifierCommand.EMERGENCY_REPAIR_EFFECT);
                ClearRepairEffects();
            }
        }

        public void ProcessRepairAura()
        {
            if (!RepairAuraActive || Spacemap == null)
                return;

            var now = DateTime.Now;
            var repairTargets = GetRepairTargets(now).ToList();
            SyncRepairEffects(repairTargets);

            if (lastRepairTick.AddSeconds(RepairAuraIntervalSeconds) >= now)
                return;

            var repairedAnyTarget = false;
            foreach (var repairTarget in repairTargets)
                repairedAnyTarget = TryRepairTarget(repairTarget, RepairAuraAmount) || repairedAnyTarget;

            if (repairedAnyTarget)
                lastRepairTick = now;
        }

        private IEnumerable<Player> GetRepairTargets(DateTime now)
        {
            return Spacemap.Characters.Values
                .OfType<Player>()
                .Where(player => player != null
                    && !player.Destroyed
                    && player.CurrentHitPoints > 0
                    && player.Position.DistanceTo(Position) <= RepairAuraRange
                    && player.LastCombatTime.AddSeconds(10) < now
                    && (player.CurrentHitPoints < player.MaxHitPoints || player.CurrentShieldPoints < player.MaxShieldPoints))
                .ToList();
        }

        private void SyncRepairEffects(IEnumerable<Player> repairTargets)
        {
            var desiredTargets = repairTargets.ToDictionary(player => player.Id, player => player);

            foreach (var activeTarget in activeRepairTargets.Where(x => !desiredTargets.ContainsKey(x.Key)).ToList())
            {
                activeTarget.Value.RemoveVisualModifier(VisualModifierCommand.HEAL_EFFECT);
                activeRepairTargets.Remove(activeTarget.Key);
            }

            foreach (var desiredTarget in desiredTargets)
            {
                if (!desiredTarget.Value.VisualModifiers.ContainsKey(VisualModifierCommand.HEAL_EFFECT))
                    desiredTarget.Value.AddVisualModifier(VisualModifierCommand.HEAL_EFFECT, 0, "", 0, true);

                activeRepairTargets[desiredTarget.Key] = desiredTarget.Value;
            }
        }

        private void ClearRepairEffects()
        {
            foreach (var activeTarget in activeRepairTargets.Values.ToList())
                activeTarget.RemoveVisualModifier(VisualModifierCommand.HEAL_EFFECT);

            activeRepairTargets.Clear();
        }

        private static bool TryRepairTarget(Player player, int repairAmount)
        {
            if (player == null || repairAmount <= 0)
                return false;

            var repaired = false;

            if (player.CurrentHitPoints < player.MaxHitPoints)
            {
                player.Heal(repairAmount);
                repaired = true;
            }

            if (player.CurrentShieldPoints < player.MaxShieldPoints)
            {
                player.Heal(repairAmount, 0, HealType.SHIELD);
                repaired = true;
            }

            return repaired;
        }

        public override void Click(GameSession gameSession)
        {
            var player = gameSession?.Player;
            if (player == null)
                return;

            player.SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.TRADE_WINDOW_ACTIVATION}|1");
            player.SendOreShopInfo();
        }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            return AssetCreateCommand.write(GetAssetType(), Name,
                0, "", Id, 0, 0,
                Position.X, Position.Y, 0, false, false, true, ShowBubble,
                new ClanRelationModule(clanRelationModule),
                VisualModifiers.Values.ToList());
        }
    }
}