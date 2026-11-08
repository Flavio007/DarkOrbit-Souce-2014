using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ow.Game.Objects;
using Ow.Game.GalaxyGates;
using Ow.Game.Objects.Players;
using Ow.Game.Objects.Players.Managers;
using Ow.Game.Movements;
using Ow.Game.Objects.Stations;
using Ow.Managers;
using Ow.Net;
using Ow.Net.netty;
using Ow.Net.netty.commands;
using Ow.Net.netty.handlers;
using Ow.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ow.Game.Objects.Collectables;
using Ow.Game.Objects.Mines;
using System.Collections.Concurrent;
using Ow.Game.Ticks;
using Ow.Game.Events;

namespace Ow.Game
{
    // Classic low-map IDs: MMO 1..4, EIC 5..8, VRU 9..12.
    internal static class LowMapRadar
    {
        public static int Sector(int map) { return map >= 1 && map <= 12 ? (map - 1) % 4 + 1 : 0; }
        public static int Owner(int map) { return Sector(map) != 0 ? (map - 1) / 4 + 1 : 0; }
        public static bool Enemy(int viewerFaction, int subjectFaction) { return viewerFaction >= 1 && viewerFaction <= 3 && subjectFaction >= 1 && subjectFaction <= 3 && viewerFaction != subjectFaction; }
        public static bool Warning(int map, int viewerFaction, int subjectFaction) { return (Sector(map) == 1 || Sector(map) == 2) && Enemy(viewerFaction, subjectFaction); }
        public static bool Shared(int map, int viewerFaction, int subjectFaction) { return Sector(map) == 1 && viewerFaction == Owner(map) && Enemy(viewerFaction, subjectFaction); }
    }

    class OptionsBase
    {
        public bool StarterMap { get; set; }
        public bool PvpMap { get; set; }
        public bool RangeDisabled { get; set; }
        public bool CloakBlocked { get; set; }
        public bool LogoutBlocked { get; set; }
        public bool DeathLocationRepair { get; set; }
    }

    class NpcsBase
    {
        public int ShipId { get; set; }
        public int Amount { get; set; }
    }

    class CollectableBase
    {
        public int TypeId { get; set; }
        public int Amount { get; set; }
        public List<int> TopLeft { get; set; }
        public List<int> BottomRight { get; set; }
        public bool Respawnable { get; set; }
    }

    class Spacemap : Tick
    {
        private static readonly bool OresEnabled = true;

        public ConcurrentDictionary<int, Character> Characters = new ConcurrentDictionary<int, Character>();
        public ConcurrentDictionary<int, Activatable> Activatables = new ConcurrentDictionary<int, Activatable>();
        public ConcurrentDictionary<int, Object> Objects = new ConcurrentDictionary<int, Object>();
        public ConcurrentDictionary<string, POI> POIs = new ConcurrentDictionary<string, POI>();

        public int Id { get; set; }
        public int VisualMapId { get; private set; }
        public bool PersistAsVisualMapId { get; private set; }
        public string Name { get; set; }
        public int FactionId { get; set; }
        public string StationsJSON { get; set; }
        public Position[] Limits { get; private set; }
        public OptionsBase Options { get; set; }
        public bool Instance { get; set; }
        public int Curwave { get; set; }
        public List<InstanceNpc> InstanceNpcs = new List<InstanceNpc>();
        private List<NpcsBase> NpcsBase { get; set; }
        private List<PortalBase> PortalBase { get; set; }
        private List<StationBase> StationBase { get; set; }
        private List<CollectableBase> CollectablesBase { get; set; }

        public Spacemap(int mapId, string name, int factionId, List<NpcsBase> npcs, List<PortalBase> portals, List<StationBase> stations, OptionsBase options, List<CollectableBase> collectables = null, int visualMapId = 0, bool persistAsVisualMapId = false)
        {
            Id = mapId;
            VisualMapId = visualMapId > 0 ? visualMapId : mapId;
            PersistAsVisualMapId = persistAsVisualMapId;
            Name = name;
            FactionId = factionId;
            NpcsBase = npcs;
            PortalBase = portals;
            StationBase = stations;
            Options = options;
            CollectablesBase = collectables;
            ParseLimits();
            LoadObjects();

            Program.TickManager.AddTick(this);
        }

        public void Tick()
        {
            CharacterTicker();
            //ObjectsTicker();
        }


        private void ParseLimits()
        {
            var effectiveMapId = GetMapBehaviorId();
            Limits = new Position[] { null, null };
            Limits[0] = new Position(0, 0);
            if (effectiveMapId == 16 || effectiveMapId == 29)
                Limits[1] = new Position(41800, 26000);
            else if (effectiveMapId == 200)
                Limits[1] = new Position(21000, 13500);
            else if (effectiveMapId == 255)
                Limits[1] = new Position(10450, 6500);
            else Limits[1] = new Position(20800, 12800);
        }

        private int GetMapBehaviorId()
        {
            return VisualMapId > 0 ? VisualMapId : Id;
        }

        public void LoadObjects()
        {   
            if (StationBase != null && StationBase.Count >= 1) {
                foreach (var station in StationBase)
                {
                    var position = new Position(station.Position[0], station.Position[1]);

                    switch (station.TypeId)
                    {
                        case AssetTypeModule.BASE_COMPANY:
                            new HomeStation(this, station.FactionId, position, GameManager.GetClan(0));
                            break;
                    }
                }
            }

            if (NpcsBase != null && NpcsBase.Count >= 1)
            {
                foreach (var npc in NpcsBase)
                {
                    if (Id == 29)
                        for (int i = 1; i <= npc.Amount; i++)
                            new Npc(Randoms.CreateRandomID(), GameManager.GetShip(npc.ShipId), this, Position.Random(this, 0, 41600, 0, 25600), 0);

                    else
                        for (int i = 1; i <= npc.Amount; i++)
                            new Npc(Randoms.CreateRandomID(), GameManager.GetShip(npc.ShipId), this, Position.Random(this, 0, 20800, 0, 12800),0);
                }
            }

            if (Id == 18 || Id == 22 || Id == 26)
            {
                new Cubikon(Randoms.CreateRandomID(), GameManager.GetShip(80), this, Position.Cube1, 81, 20);
                new Cubikon(Randoms.CreateRandomID(), GameManager.GetShip(80), this, Position.Cube2, 81, 20);
                new Cubikon(Randoms.CreateRandomID(), GameManager.GetShip(80), this, Position.Cube3, 81, 20);
                new Cubikon(Randoms.CreateRandomID(), GameManager.GetShip(80), this, Position.Cube4, 81, 20);
            }

            if (Id == 29)
            {
                new Cubikon(Randoms.CreateRandomID(), GameManager.GetShip(480), this, Position.GetPosOnCircle(Position.SecretPortal, 6000), 481, 20);
                new Cubikon(Randoms.CreateRandomID(), GameManager.GetShip(480), this, Position.GetPosOnCircle(Position.SecretPortal, 6000), 481, 20);
                new Cubikon(Randoms.CreateRandomID(), GameManager.GetShip(480), this, Position.GetPosOnCircle(Position.SecretPortal, 6000), 481, 20);
                new Cubikon(Randoms.CreateRandomID(), GameManager.GetShip(480), this, Position.GetPosOnCircle(Position.SecretPortal, 6000), 481, 20);
                new Cubikon(Randoms.CreateRandomID(), GameManager.GetShip(880), this, Position.GetPosOnCircle(Position.SecretPortal, 1000), 881, 20);
            }

                if (Id == 58)
                new SolarLordakium(Randoms.CreateRandomID(), GameManager.GetShip(107), this, Position.Random(this, 0,20800,0,12800), 0);

            if (PortalBase != null && PortalBase.Count >= 1)
            {
                foreach (var portal in PortalBase)
                {
                    var portalPosition = new Position(portal.Position[0], portal.Position[1]);
                    var targetPosition = new Position(portal.TargetPosition[0], portal.TargetPosition[1]);
                    new Portal(this, portalPosition, targetPosition, portal.TargetSpaceMapId, portal.GraphicId, portal.FactionId, portal.Visible, portal.Working, false);
                }
            }

            if (Id == 29)
                new Portal(this, Position.SecretPortal, Position.SecretPortal, 1, -1, 0, false, true, true);


            if (IsBonusBoxMap())
            {
                var bonusBoxAmount = new[] { 13, 14, 15 }.Contains(Id) ? 125 : 50;

                for (int i = 1; i <= bonusBoxAmount; i++)
                    new BonusBox(Position.Random(this, 0, 20800, 0, 12800), this, true);
                //for (int i = 0; i <= 25; i++)
                //    new GreenBooty(Position.Random(this, 0, 20800, 0, 12800), this, true);
            }

            LoadConfiguredCollectables();

            if (Id == 101)
            {
                var poi = new POI("jackpot_poi", POITypes.RADIATION, POIDesigns.SIMPLE, POIShapes.CIRCLE, new List<Position> { new Position(5000, 3200), new Position(2250, 950) }, true, true);
                POIs.TryAdd("jackpot_poi", poi);
            }

            if (Id == 121)
            {
                var poi1 = new POI("uba_poi1", POITypes.NO_ACCESS, POIDesigns.SIMPLE, POIShapes.RECTANGLE, new List<Position> { new Position(5000, 3000), new Position(4000, 4000), new Position(4000, 2000), new Position(6000, 2000) }, true, true);
                var poi2 = new POI("uba_poi2", POITypes.NO_ACCESS, POIDesigns.SIMPLE, POIShapes.CIRCLE, new List<Position> { new Position(4400, 3600), new Position(200, 100) }, true, true);
                var poi3 = new POI("uba_poi3", POITypes.NO_ACCESS, POIDesigns.SIMPLE, POIShapes.CIRCLE, new List<Position> { new Position(5600, 2400), new Position(200, 100) }, true, true);

                POIs.TryAdd("uba_poi1", poi1);
                POIs.TryAdd("uba_poi2", poi2);
                POIs.TryAdd("uba_poi3", poi3);
            }
        }

        private bool IsBonusBoxMap()
        {
            return Id >= 1 && Id < 29 && Id != 16; // if it's lower than 4-5 and not 4-4
        }

        private void LoadConfiguredCollectables()
        {
            if (!OresEnabled) return;

            if (CollectablesBase == null || CollectablesBase.Count < 1) return;

            var mineralVeins = new List<Tuple<int, Position>>();
            var mineralPositions = new List<Position>();
            foreach (var collectable in CollectablesBase)
            {
                if (collectable == null || collectable.Amount <= 0) continue;
                if (!Ore.TryResolveOre(collectable.TypeId, out var oreType, out _)) continue;

                int minX;
                int maxX;
                int minY;
                int maxY;
                GetCollectableBounds(collectable.TopLeft, collectable.BottomRight, out minX, out maxX, out minY, out maxY);
                if (collectable.TypeId >= 1 && collectable.TypeId <= 3)
                {
                    // Generate once per map load. Each mineral keeps its place in its vein on respawn.
                    foreach (var position in GenerateMineralVeins(collectable.TypeId, collectable.Amount,
                        minX, maxX, minY, maxY, mineralVeins, mineralPositions))
                        new Ore(collectable.TypeId, oreType, position, this, collectable.Respawnable,
                            position.X, position.X, position.Y, position.Y);
                    continue;
                }
                for (int i = 0; i < collectable.Amount; i++)
                {
                    var spawnPosition = RandomPositionInBounds(minX, maxX, minY, maxY);
                    new Ore(collectable.TypeId, oreType, spawnPosition, this, collectable.Respawnable, minX, maxX, minY, maxY);
                }
            }
        }

        // Ordinary ores only: 8-16 deposits per vein, 480-unit radius, separated mineral types.
        internal List<Position> GenerateMineralVeins(int type, int amount, int minX, int maxX, int minY, int maxY,
            List<Tuple<int, Position>> veins, List<Position> occupied)
        {
            const int radius = 480, portalClearance = 1800, spacing = 85;
            var result = new List<Position>();
            var portals = Activatables.Values.OfType<Portal>().Where(p => p.Visible && !p.Random).ToArray();
            Func<Position, Position, double> distanceSquared = (a, b) =>
                (double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y);
            Func<Position, bool> safe = p => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY
                && portals.All(portal => distanceSquared(p, portal.Position) >= portalClearance * portalClearance);
            while (result.Count < amount)
            {
                Position center = null;
                for (int attempt = 0; attempt < 1024; attempt++)
                {
                    var candidate = RandomPositionInBounds(minX, maxX, minY, maxY);
                    if (!safe(candidate) || veins.Any(v => distanceSquared(candidate, v.Item2) <
                        (v.Item1 == type ? 900 * 900 : 1300 * 1300))) continue;
                    center = candidate;
                    break;
                }
                // Never fall back to spawning on a portal or mixing veins in a cramped configured region.
                if (center == null) break;
                veins.Add(Tuple.Create(type, center));
                var remaining = amount - result.Count;
                var veinSize = remaining <= 16 ? remaining : Randoms.random.Next(8, 17);
                if (remaining > veinSize && remaining - veinSize < 8) veinSize = remaining - 8;
                int placed = 0;
                for (int attempt = 0; attempt < 2048 && placed < veinSize; attempt++)
                {
                    var angle = Randoms.random.NextDouble() * Math.PI * 2;
                    var r = Math.Sqrt(Randoms.random.NextDouble()) * radius;
                    var position = new Position(center.X + (int)(Math.Cos(angle) * r), center.Y + (int)(Math.Sin(angle) * r));
                    if (!safe(position) || occupied.Any(p => distanceSquared(p, position) < spacing * spacing)) continue;
                    result.Add(position);
                    occupied.Add(position);
                    placed++;
                }
                if (placed == 0) break;
            }
            if (result.Count != amount)
                Console.WriteLine($"[Mineral veins] Map {Id}, ore {type}: {result.Count}/{amount} safe deposits (configured region too small).");
            return result;
        }

        private void GetCollectableBounds(List<int> topLeft, List<int> bottomRight, out int minX, out int maxX, out int minY, out int maxY)
        {
            var x1 = topLeft != null && topLeft.Count > 0 ? topLeft[0] : Limits[0].X;
            var y1 = topLeft != null && topLeft.Count > 1 ? topLeft[1] : Limits[0].Y;
            var x2 = bottomRight != null && bottomRight.Count > 0 ? bottomRight[0] : Limits[1].X;
            var y2 = bottomRight != null && bottomRight.Count > 1 ? bottomRight[1] : Limits[1].Y;

            minX = ClampToMap(Math.Min(x1, x2), true);
            maxX = ClampToMap(Math.Max(x1, x2), true);
            minY = ClampToMap(Math.Min(y1, y2), false);
            maxY = ClampToMap(Math.Max(y1, y2), false);
        }

        private Position RandomPositionInBounds(int minX, int maxX, int minY, int maxY)
        {
            if (minX == maxX && minY == maxY)
                return new Position(minX, minY);

            var x = minX == maxX ? minX : Randoms.random.Next(minX, maxX);
            var y = minY == maxY ? minY : Randoms.random.Next(minY, maxY);
            return new Position(x, y);
        }

        private int ClampToMap(int value, bool xAxis)
        {
            var min = xAxis ? Limits[0].X : Limits[0].Y;
            var max = xAxis ? Limits[1].X : Limits[1].Y;
            return Math.Max(min, Math.Min(max, value));
        }

        private readonly ConcurrentDictionary<int, int> threatCounts = new ConcurrentDictionary<int, int>();
        public int EnemyPlayerCount(Player viewer)
        {
            if (viewer == null || LowMapRadar.Sector(VisualMapId) == 0) return 0;
            return Characters.Values.OfType<Player>().Count(p => p != viewer && !p.Destroyed && ContainsCharacter(p) && LowMapRadar.Enemy(viewer.FactionId, p.FactionId));
        }
        private void SendThreat(Player viewer, int count, bool force = false)
        {
            if (!force && threatCounts.TryGetValue(viewer.Id, out var old) && old == count) return;
            threatCounts[viewer.Id] = count;
            // Flash reads the capped level; WASM also reads the exact count.
            viewer.SendPacket("0|n|w|" + Math.Min(5, count) + "|" + count);
        }

        public void CharacterTicker()
        {
            var lowMapPlayers = LowMapRadar.Sector(VisualMapId) == 0 ? new Player[0] : Characters.Values.OfType<Player>().Where(p => !p.Destroyed && ContainsCharacter(p)).ToArray();
            foreach (var character in Characters.Values)
            {
                foreach (var otherCharacter in Characters.Values.Where(x => x.Id != character.Id && !x.Equals(character)))
                {
                    if (CanSeeCharacter(character, otherCharacter))
                        character.AddInRangeCharacter(otherCharacter);
                    else
                        character.RemoveInRangeCharacter(otherCharacter);
                }

                if (character is Player player)
                {
                    if (LowMapRadar.Sector(VisualMapId) != 0) SendThreat(player, lowMapPlayers.Count(p => p != player && LowMapRadar.Enemy(player.FactionId, p.FactionId)));
                    foreach (var obj in Objects.Values)
                    {
                        if (obj is Collectable collectable)
                        {
                            if (!(collectable.ToPlayer == null || (collectable.ToPlayer != null && player == collectable.ToPlayer))) continue;
                        }
                        else if (obj is Mine mine)
                        {
                            if (!(player == mine.Player || !Duel.InDuel(player) || (Duel.InDuel(player) && player.Storage.Duel?.GetOpponent(player) == mine.Player))) continue;
                        }

                        if (player.Position.DistanceTo(obj.Position) <= 1250)
                        {
                            if (!player.Storage.InRangeObjects.ContainsKey(obj.Id))
                            {
                                player.Storage.InRangeObjects.TryAdd(obj.Id, obj);

                                if (obj is Collectable)
                                    player.SendCommand((obj as Collectable).GetCollectableCreateCommand());
                                else if (obj is Mine)
                                    player.SendCommand((obj as Mine).GetMineCreateCommand());
                                else if (obj is Asset)
                                    player.SendCommand((obj as Asset).GetAssetCreateCommand());
                            }
                            else
                            {
                                if (obj is Mine )
                                {
                                    var mine = obj as Mine;
                                    if (mine.Active && mine.activationTime.AddMilliseconds(Mine.ACTIVATION_TIME) < DateTime.Now)
                                    {
                                        if (player.Position.DistanceTo(mine.Position) < Mine.RANGE)
                                        {
                                            mine.Remove();
                                            mine.Explode();
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (player.Storage.InRangeObjects.ContainsKey(obj.Id))
                            {
                                player.Storage.InRangeObjects.TryRemove(obj.Id, out var outObj);

                                if (obj is Collectable)
                                    player.SendCommand(DisposeBoxCommand.write((obj as Collectable).Hash, true));
                                else if (obj is Mine)
                                    player.SendPacket($"0|{ServerCommands.REMOVE_ORE}|{(obj as Mine).Hash}");
                                else if (obj is Asset)
                                    player.SendCommand(AssetRemoveCommand.write(new AssetTypeModule((obj as Asset).AssetTypeId), obj.Id));
                            }
                        }
                    }

                    if (CheckRadiation(player) || CheckActivatables(player))
                        player.SendCommand(player.GetBeaconCommand());
                }
            }
        }


        private void ObjectsTicker()
        {
            /*
            foreach (var obj in Objects)
            {
                obj.Value?.Tick();
            }
            */
        }

        public bool CheckActivatables(Player Player)
        {
            bool isInSecureZone = false;
            bool inEquipZone = false;
            bool onBlockedMinePosition = false;
            bool isInSafeZone = false;

            foreach (var entity in Activatables.Values)
            {
                if (entity is Portal hiddenPortal && !hiddenPortal.IsVisibleTo(Player))
                {
                    Player.UpdateActivatable(entity, false);
                    continue;
                }

                bool inRange = Player.Position.DistanceTo(entity.Position) <= (entity is HomeStation ? HomeStation.SECURE_ZONE_RANGE : 700);
                short status = inRange ? MapAssetActionAvailableCommand.ON : MapAssetActionAvailableCommand.OFF;

                if (inRange)
                {
                    if (!(entity is BattleStation) && !(entity is Satellite) && !(entity is OreTradeStation embeddedTrade && embeddedTrade.FactionStation != null))
                        onBlockedMinePosition = true;

                    if (entity is HomeStation homeStation)
                    {
                        if (homeStation.FactionId == Player.FactionId)
                        {
                            if (!Player.LastAttackTime(5))
                            {
                                isInSecureZone = true;
                                inEquipZone = true;
                            }
                        }
                    }
                    if (entity is Portal portal)
                    {
                        if (portal.FactionId == Player.FactionId)
                        {
                            if (!Player.LastAttackTime(10))
                            {
                                isInSecureZone = true;
                            }
                        }
                        if(Id != 29)
                            isInSafeZone = true;
                    }
                    else if (entity is RepairStation)
                    {
                        if (Player.CurrentHitPoints == Player.MaxHitPoints || Player.FactionId != entity.FactionId)
                            inRange = false;
                    }
                }

                bool activateButton = Player.UpdateActivatable(entity, inRange);

                if (activateButton)
                {
                    if (entity is Portal portal && !portal.Working)
                        status = MapAssetActionAvailableCommand.OFF;
                    if ((entity is QuestGiverStation || entity is OreTradeStation) && entity.FactionId != Player.FactionId)
                        status = MapAssetActionAvailableCommand.OFF;

                    if (entity is OreTradeStation factionTrade && factionTrade.FactionStation != null && !factionTrade.CanTrade(Player))
                        status = MapAssetActionAvailableCommand.OFF;

                    //TODO check old = if (entity is BattleStation battleStation && battleStation.Clan.Id != 0 && !battleStation.InBuildingState && battleStation.Clan.Id != Player.Clan.Id)

                    if (entity is BattleStation battleStation && battleStation.Clan.Id != 0 && battleStation.Clan.Id != Player.Clan.Id)
                        status = MapAssetActionAvailableCommand.OFF;

                    if (entity is BattleStation && status == MapAssetActionAvailableCommand.OFF)
                    {                      
                        //TODO: Find close ui command end send it
                    }

                    var actionTooltip = new List<ClientUITooltipModule>();

                    if (entity is Portal)
                    {
                        actionTooltip.Add(new ClientUITooltipModule(
                            new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED),
                            ClientUITooltipModule.STANDARD,
                            "q2_condition_JUMP",
                            new List<ClientUITextReplacementModule>()));
                    }
                    else if (entity is QuestGiverStation)
                    {
                        actionTooltip.Add(new ClientUITooltipModule(
                            new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED),
                            ClientUITooltipModule.STANDARD,
                            "mapasset_questgiver",
                            new List<ClientUITextReplacementModule>()));
                    }

                    var assetAction =
                            MapAssetActionAvailableCommand.write(entity.Id,
                                                               status,
                                                               inRange,
                                                               new ClientUITooltipsCommand(actionTooltip),
                                                               new class_h45()
                            );

                    Player.SendCommand(assetAction);
                }
            }

            if (Player.Storage.OnBlockedMinePosition != onBlockedMinePosition)
                Player.Storage.OnBlockedMinePosition = onBlockedMinePosition;

            if (Player.Storage.IsInEquipZone != inEquipZone)
            {
                Player.Storage.IsInEquipZone = inEquipZone;

                Player.SendCommand(EquipReadyCommand.write(Player.Storage.IsInEquipZone));

                var packet = Player.Storage.IsInEquipZone ? "0|A|STM|msg_equip_ready" : "0|A|STM|msg_equip_not_ready";
                Player.SendPacket(packet);
            }

            if (Player.Storage.IsInDemilitarizedZone != isInSecureZone)
            {
                Player.Storage.IsInDemilitarizedZone = isInSecureZone;
                return true;
            }

            if (Player.Storage.IsInSafeZone != isInSafeZone){
                Player.Storage.IsInSafeZone = isInSafeZone;
                return true;
            }
            return false;
        }

        public bool CheckRadiation(Player Player)
        {
            var effectiveMapId = GetMapBehaviorId();
            int positionX = Player.Position.X;
            int positionY = Player.Position.Y;

            bool inRadiationZone = false;

            if (effectiveMapId == 255)
                inRadiationZone = positionX < 0 || positionX > 10450 || positionY < 0 || positionY > 6500;
            else if (effectiveMapId == 200)
                inRadiationZone = positionX < 0 || positionX > 21000 || positionY < 0 || positionY > 13500;
            else if (effectiveMapId == 16 || effectiveMapId == 29 || effectiveMapId == 42)
                inRadiationZone = positionX < 0 || positionX > 41800 || positionY < 0 || positionY > 26000;
            else
                inRadiationZone = positionX < 0 || positionX > 20900 || positionY < 0 || positionY > 13000;

            foreach (var poi in POIs.Values)
            {
                if (poi.Active && poi.Type == POITypes.RADIATION && poi.Contains(positionX, positionY))
                    inRadiationZone = true;
            }

            if (Player.Storage.IsInRadiationZone != inRadiationZone)
            {
                Player.Storage.IsInRadiationZone = inRadiationZone;
                return true;
            }
            return false;
        }

        public void SendObjects(Player player)
        {
            PacketDebug.NotifyPortalCount(player, Id, Activatables.Values.OfType<Portal>().Count());

            foreach (var activatable in Activatables.Values)
            {
                if (activatable is Portal portal && !portal.IsVisibleTo(player))
                    continue;

                short relationType = player.Clan.Id != 0 && activatable.Clan.Id != 0 ? activatable.Clan.GetRelation(player.Clan) : (short)0;
                player.SendCommand(activatable.GetAssetCreateCommand(relationType));

                if (activatable is BattleStation battleStation)
                    battleStation.SendStatusCommand(player);
            }

            foreach (var poi in POIs.Values)
            {
                var poiCommand = poi.GetPOICreateCommand();
                if (Id == 16 && poi.Type == POITypes.SECTOR_CONTROL_SECTOR_ZONE)
                {
                    var sectorControlStation = Activatables.Values
                        .OfType<BattleStation>()
                        .FirstOrDefault(station => station.SectorControlHash == poi.Id);
                    if (sectorControlStation != null)
                    {
                        // Serialize beacon registration with range entry and
                        // suppress duplicate MapAddPOI commands per player.
                        sectorControlStation.SendSectorControlPOI(player, poiCommand, "MAP_OBJECTS");
                        continue;
                    }
                }

                player.SendCommand(poiCommand);
                if (Id == 16 && poi.Type == POITypes.SECTOR_CONTROL_SECTOR_ZONE)
                {
                    if (PacketDebug.Enabled)
                    {
                        player.SendPacket(
                            $"0|A|STD|[PACKET_DEBUG] AFTER MapAddPOICommand ID={MapAddPOICommand.ID} LENGTH={poiCommand.Length} MAP={Id} HASH={poi.Id} REASON=MAP_OBJECTS");
                    }
                }
            }

            foreach (var obj in Objects.Values)
                if (obj is Asset asset)
                    player.SendCommand(asset.GetAssetCreateCommand());
        }

        public void SendCharacters(Player player)
        {
            if (player == null)
                return;

            SendThreat(player, EnemyPlayerCount(player), true);
            foreach (var character in Characters.Values.ToList())
            {
                if (character == null || character.Id == player.Id || character.Destroyed)
                    continue;

                if (CanSeeCharacter(player, character))
                    player.AddInRangeCharacter(character);
            }
        }

        public void AddAndInitPlayer(Player player, bool sendSettings = false)
        {
            var petActivated = player.Pet != null && player.Pet.Activated;

            if (petActivated)
                player.Pet.Deactivate(true);

            AddCharacter(player);
            LoginRequestHandler.SendPlayer(player);

            if (petActivated)
                player.Pet.Activate();

            if (sendSettings)
                LoginRequestHandler.SendSettings(player);
        }

        public Activatable GetActivatableMapEntity(int pAssetId)
        {
            return !Activatables.ContainsKey(pAssetId) ? null : Activatables[pAssetId];
        }

        public class CharacterArgs : EventArgs
        {
            public Character Character { get; }
            public CharacterArgs(Character character)
            {
                Character = character;
            }
        }

        public event EventHandler<CharacterArgs> CharacterRemoved;
        public event EventHandler<CharacterArgs> CharacterAdded;

        public bool AddCharacter(Character character)
        {
            var success = Characters.TryAdd(character.Id, character);
            if (success)
            {
                CharacterAdded?.Invoke(this, new CharacterArgs(character));
            }
            return success;
        }

        public bool ContainsCharacter(Character character)
        {
            return character != null && character.Spacemap == this
                && Characters.TryGetValue(character.Id, out var current) && ReferenceEquals(current, character);
        }

        public bool CanSeeCharacter(Character viewer, Character subject)
        {
            if (viewer == subject || viewer == null || subject == null || viewer.Destroyed || subject.Destroyed
                || !ContainsCharacter(viewer) || !ContainsCharacter(subject)) return false;
            var range = viewer.RenderRange == -1 || subject.RenderRange == -1
                ? -1 : Math.Max(viewer.RenderRange, subject.RenderRange);
            // Tutorial radar sees every ship; combat and NPC aggro keep their own ranges.
            return (viewer is Player defender && subject is Player intruder && LowMapRadar.Shared(VisualMapId, defender.FactionId, intruder.FactionId))
                || (viewer is Player && VisualMapId == Events.TutorialManager.TutorialMapId)
                || viewer.AllMapRange || subject.AllMapRange || viewer.InRange(subject, range);
        }

        public bool RemoveCharacter(Character character)
        {
            var success = ((ICollection<KeyValuePair<int, Character>>)Characters).Remove(new KeyValuePair<int, Character>(character.Id, character));
            if (success)
            {
                if (character is Player leavingPlayer)
                {
                    threatCounts.TryRemove(leavingPlayer.Id, out var ignoredThreat);
                    foreach (var battleStation in Activatables.Values.OfType<BattleStation>())
                        battleStation.HideSectorControlProgressBar(leavingPlayer);
                }

                CharacterRemoved?.Invoke(this, new CharacterArgs(character));

                foreach (var otherCharacter in Characters.Values)
                {
                    if (otherCharacter.Selected == character)
                        otherCharacter.Deselection();
                    otherCharacter.RemoveInRangeCharacter(character);
                }
                character.Deselection();
                foreach (var visible in character.InRangeCharacters.Values)
                    character.RemoveInRangeCharacter(visible);
            }
            return success;
        }
    }
}
