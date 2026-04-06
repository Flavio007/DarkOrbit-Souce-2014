using Ow.Game.Movements;
using Ow.Game.Objects;
using Ow.Game.Objects.Players;
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
    class GroupMap200Manager
    {
        private sealed class GroupMapInstance
        {
            public int Key { get; set; }
            public int MapId { get; set; }
            public Spacemap Spacemap { get; set; }
            public GroupMapExitPortal ExitPortal { get; set; }
            public List<GroupMapRelayStation> Relays { get; } = new List<GroupMapRelayStation>();
            public int CurrentRelayIndex { get; set; }
        }

        public const int VisualMapId = 200;
        private const int EntryPortalGraphicId = 34;
        private const int DynamicMapStartId = 20500;
        private const int FutureMinimumGroupMembers = 3;
        private const int FutureWaitingTimeSeconds = 294;

        private static readonly Position BasePortalPosition = new Position(Position.InvasionGatePosition.X, Position.InvasionGatePosition.Y);
        private static readonly Position InstanceSpawnPosition = new Position(1000, 12000);
        private static readonly Position ExitPortalPosition = new Position(1700, 12000);
        private static readonly Position OreTradePosition = new Position(10000, 5500);
        private static readonly Position[] RelayPositions =
        {
            new Position(2750, 1750),
            new Position(18250, 4200),
            new Position(18250, 11750),
            new Position(6000, 11750)
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
                initialized = true;
            }
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
                RangeDisabled = false,
                CloakBlocked = false,
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
                instance.Relays.Add(new GroupMapRelayStation(this, instance.Spacemap, new Position(relayPosition.X, relayPosition.Y), index));
            }

            new GroupMapOreTradeStation(instance.Spacemap, new Position(OreTradePosition.X, OreTradePosition.Y));
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
                targetMap.CheckActivatables(player);
            }
            finally
            {
                player.Storage.Jumping = false;
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
                    instance.CurrentRelayIndex++;

                return chargeAmount;
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

        public override void Tick()
        {
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
                new List<VisualModifierCommand>());
        }
    }
}