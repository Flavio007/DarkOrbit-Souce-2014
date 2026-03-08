using Ow.Game.Movements;
using Ow.Game.Objects.Players;
using Ow.Managers;
using Ow.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Ow.Game.Objects.AI
{
    class AIShips
    {
        private class FakeShipConfig
        {
            public string Name { get; set; }
            public string ClanName { get; set; }
            public string ClanTag { get; set; }
            public int FactionId { get; set; }
            public int ShipId { get; set; }
            public int RankId { get; set; }
            public int Level { get; set; }
            public string Ability { get; set; }
            public string Objective { get; set; }
            public Dictionary<string, int> AvailableAmmo { get; set; }
            public int EmpAmount { get; set; }
            public int IshAmount { get; set; }
            public int SmbAmount { get; set; }
            public List<Drones> Drones { get; set; }

            public FakeShipConfig()
            {
                Name = "";
                ClanName = "";
                ClanTag = "";
                Ability = "";
                Objective = "hunt_npcs";
                AvailableAmmo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                Drones = new List<Drones>();
            }
        }

        private class FakePlayerController
        {
            public Position SpawnPosition { get; set; }
            public Position Destination { get; set; }
            public bool DestinationReached { get; set; }
            public bool AutoPatrol { get; set; }
            public bool AttackNpcs { get; set; }
            public int TargetId { get; set; }
            public DateTime NextDecisionTime { get; set; }
            public string Objective { get; set; }
            public string Ability { get; set; }
        }

        private static readonly ConcurrentDictionary<int, FakePlayer> FakePlayers = new ConcurrentDictionary<int, FakePlayer>();
        private static readonly ConcurrentDictionary<int, FakePlayerController> Controllers = new ConcurrentDictionary<int, FakePlayerController>();
        private static int nextDynamicClanId = -1000;

        private const int PatrolRadius = 1800;
        private const int TargetOrbitDistance = 550;
        private const int DestinationTolerance = 120;
        private const int RepathTolerance = 200;
        private const string DefaultConfigRelativePath = "config\\fake_ships.xml";

        public static void LoadConfiguredFakeShips(string path = null)
        {
            var resolvedPath = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultConfigRelativePath)
                : path;

            EnsureConfigFileExists(resolvedPath);

            if (!File.Exists(resolvedPath))
                return;

            try
            {
                var document = XDocument.Load(resolvedPath);
                var ships = ParseFakeShipConfigs(document);
                foreach (var fakeShip in ships)
                    SpawnConfiguredFakeShip(fakeShip);
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [AIShips.cs] LoadConfiguredFakeShips exception: {e}");
            }
        }

        public static FakePlayer CreateStationaryFakePlayer(Ship ship, Spacemap map, Position nearPosition, int factionId)
        {
            return CreateConfiguredFakePlayer(ship, map, nearPosition, factionId, null, null, 1, 1, "", "hunt_npcs", null, 0, 0, 0, null);
        }

        public static FakePlayer CreateConfiguredFakePlayer(
            Ship ship,
            Spacemap map,
            Position nearPosition,
            int factionId,
            string name,
            Clan clan,
            int rankId,
            int level,
            string ability,
            string objective,
            Dictionary<string, int> availableAmmo,
            int empAmount,
            int ishAmount,
            int smbAmount,
            List<Drones> drones)
        {
            if (ship == null || map == null || nearPosition == null)
                return null;

            var id = CreateUniqueId(map);
            var fakePlayerName = string.IsNullOrWhiteSpace(name) ? "" + id : name.Trim();
            var spawn = BuildSpawnPosition(map, nearPosition);

            var fakePlayer = new FakePlayer(id, fakePlayerName, ship, map, spawn, factionId);
            fakePlayer.ApplyProfile(fakePlayerName, clan, rankId, level, ability, availableAmmo, empAmount, ishAmount, smbAmount, drones);
            FakePlayers[id] = fakePlayer;

            var normalizedObjective = NormalizeObjective(objective);
            Controllers[id] = new FakePlayerController
            {
                SpawnPosition = new Position(spawn.X, spawn.Y),
                Destination = new Position(spawn.X, spawn.Y),
                DestinationReached = true,
                AutoPatrol = normalizedObjective == "patrol" || normalizedObjective == "hunt_npcs",
                AttackNpcs = normalizedObjective == "hunt_npcs",
                TargetId = 0,
                NextDecisionTime = DateTime.MinValue,
                Objective = normalizedObjective,
                Ability = ability ?? ""
            };
            return fakePlayer;
        }

        public static bool MoveTo(int id, Position destination, bool autoPatrol = false)
        {
            FakePlayer fakePlayer;
            FakePlayerController controller;
            if (!FakePlayers.TryGetValue(id, out fakePlayer) || !Controllers.TryGetValue(id, out controller) || destination == null)
                return false;

            var clamped = ClampToMap(fakePlayer.Spacemap, destination);
            controller.Destination = clamped;
            controller.DestinationReached = false;
            controller.AutoPatrol = autoPatrol;
            controller.TargetId = 0;
            fakePlayer.DisableAttack(fakePlayer.Settings.InGameSettings.selectedLaser);
            fakePlayer.Selected = null;
            Movement.Move(fakePlayer, clamped);
            return true;
        }

        public static bool SetNpcAttackEnabled(int id, bool enabled)
        {
            FakePlayerController controller;
            if (!Controllers.TryGetValue(id, out controller))
                return false;

            controller.AttackNpcs = enabled;
            if (!enabled)
                controller.TargetId = 0;
            return true;
        }

        public static IReadOnlyCollection<FakePlayer> GetAll()
        {
            return FakePlayers.Values.ToList();
        }

        public static void Tick(FakePlayer fakePlayer)
        {
            if (fakePlayer == null || fakePlayer.Destroyed || fakePlayer.Spacemap == null)
                return;

            FakePlayerController controller;
            if (!Controllers.TryGetValue(fakePlayer.Id, out controller))
                return;

            var now = DateTime.Now;
            if (controller.NextDecisionTime == DateTime.MinValue)
                controller.NextDecisionTime = now;

            if (now < controller.NextDecisionTime)
                return;

            var target = ResolveTarget(fakePlayer, controller);
            if (target == null && controller.AttackNpcs)
                target = AcquireNearestNpc(fakePlayer);

            if (target != null)
            {
                controller.TargetId = target.Id;
                controller.DestinationReached = false;
                HandleNpcCombat(fakePlayer, controller, target);
            }
            else
            {
                controller.TargetId = 0;
                HandleMovement(fakePlayer, controller);
            }

            controller.NextDecisionTime = now.AddMilliseconds(Randoms.random.Next(250, 500));
        }

        public static bool Remove(int id)
        {
            FakePlayer fakePlayer;
            if (!FakePlayers.TryRemove(id, out fakePlayer))
                return false;

            Controllers.TryRemove(id, out _);

            Program.TickManager.RemoveTick(fakePlayer);
            fakePlayer.Spacemap?.RemoveCharacter(fakePlayer);
            fakePlayer.Destroyed = true;
            return true;
        }

        private static int CreateUniqueId(Spacemap map)
        {
            var id = Randoms.CreateRandomID();
            while (map.Characters.ContainsKey(id))
                id = Randoms.CreateRandomID();
            return id;
        }

        private static Position BuildSpawnPosition(Spacemap map, Position nearPosition)
        {
            var minX = map.Limits[0].X;
            var minY = map.Limits[0].Y;
            var maxX = map.Limits[1].X;
            var maxY = map.Limits[1].Y;

            var x = nearPosition.X + Randoms.random.Next(-600, 601);
            var y = nearPosition.Y + Randoms.random.Next(-600, 601);

            if (x < minX) x = minX + 100;
            if (y < minY) y = minY + 100;
            if (x > maxX) x = maxX - 100;
            if (y > maxY) y = maxY - 100;

            return new Position(x, y);
        }

        private static void HandleNpcCombat(FakePlayer fakePlayer, FakePlayerController controller, Npc target)
        {
            if (target == null)
                return;

            fakePlayer.Selected = target;

            if (!fakePlayer.AttackManager.Attacking)
                fakePlayer.EnableAttack(fakePlayer.Settings.InGameSettings.selectedLaser);

            var desiredPosition = ClampToMap(fakePlayer.Spacemap, Position.GetPosOnCircle(target.Position, TargetOrbitDistance));
            var shouldMove =
                !fakePlayer.InRange(target, fakePlayer.AttackRange - 25) &&
                (!fakePlayer.Moving || fakePlayer.Destination == null || fakePlayer.Destination.DistanceTo(desiredPosition) > RepathTolerance);

            if (shouldMove)
                Movement.Move(fakePlayer, desiredPosition);
        }

        private static void HandleMovement(FakePlayer fakePlayer, FakePlayerController controller)
        {
            if (fakePlayer.AttackManager.Attacking)
                fakePlayer.DisableAttack(fakePlayer.Settings.InGameSettings.selectedLaser);

            if (fakePlayer.Selected is Npc)
                fakePlayer.Selected = null;

            if (controller.Destination == null)
            {
                controller.Destination = new Position(controller.SpawnPosition.X, controller.SpawnPosition.Y);
                controller.DestinationReached = true;
            }

            if (!controller.DestinationReached && fakePlayer.Position.DistanceTo(controller.Destination) <= DestinationTolerance)
                controller.DestinationReached = true;

            if (!controller.DestinationReached)
            {
                if (!fakePlayer.Moving || fakePlayer.Destination == null || fakePlayer.Destination.DistanceTo(controller.Destination) > RepathTolerance)
                    Movement.Move(fakePlayer, controller.Destination);
                return;
            }

            if (controller.AutoPatrol && !fakePlayer.Moving)
            {
                controller.Destination = BuildPatrolPosition(fakePlayer.Spacemap, controller.SpawnPosition);
                controller.DestinationReached = false;
                Movement.Move(fakePlayer, controller.Destination);
            }
        }

        private static Npc ResolveTarget(FakePlayer fakePlayer, FakePlayerController controller)
        {
            if (controller.TargetId <= 0)
                return null;

            Character character;
            if (!fakePlayer.InRangeCharacters.TryGetValue(controller.TargetId, out character))
                return null;

            var npc = character as Npc;
            if (npc == null || npc.Destroyed || npc.Spacemap == null || npc.Spacemap.Id != fakePlayer.Spacemap.Id)
                return null;

            return npc;
        }

        private static Npc AcquireNearestNpc(FakePlayer fakePlayer)
        {
            return fakePlayer.InRangeCharacters.Values
                .OfType<Npc>()
                .Where(npc => npc != null && !npc.Destroyed && npc.Spacemap != null && npc.Spacemap.Id == fakePlayer.Spacemap.Id)
                .OrderBy(npc => fakePlayer.Position.DistanceTo(npc.Position))
                .FirstOrDefault();
        }

        private static Position BuildPatrolPosition(Spacemap map, Position center)
        {
            var x = center.X + Randoms.random.Next(-PatrolRadius, PatrolRadius + 1);
            var y = center.Y + Randoms.random.Next(-PatrolRadius, PatrolRadius + 1);
            return ClampToMap(map, new Position(x, y));
        }

        private static Position ClampToMap(Spacemap map, Position position)
        {
            if (map == null || position == null)
                return position;

            var minX = map.Limits[0].X;
            var minY = map.Limits[0].Y;
            var maxX = map.Limits[1].X;
            var maxY = map.Limits[1].Y;

            var x = position.X;
            var y = position.Y;

            if (x < minX) x = minX + 100;
            if (y < minY) y = minY + 100;
            if (x > maxX) x = maxX - 100;
            if (y > maxY) y = maxY - 100;

            return new Position(x, y);
        }

        private static List<FakeShipConfig> ParseFakeShipConfigs(XDocument document)
        {
            var result = new List<FakeShipConfig>();
            var root = document?.Root;
            if (root == null)
                return result;

            foreach (var shipElement in root.Elements("FakeShip"))
            {
                var config = new FakeShipConfig
                {
                    Name = GetValue(shipElement, "Name"),
                    FactionId = GetIntValue(shipElement, "Faction", 1),
                    ShipId = GetIntValue(shipElement, "ShipId", Ship.GOLIATH),
                    Ability = GetValue(shipElement, "Ability"),
                    Objective = GetValue(shipElement, "Objective", "hunt_npcs"),
                    RankId = GetIntValue(shipElement, "Rank", 1),
                    Level = GetIntValue(shipElement, "Level", 1),
                    Drones = ReadDrones(shipElement.Element("Drones"))
                };

                var clanElement = shipElement.Element("Clan");
                if (clanElement != null)
                {
                    config.ClanName = GetAttributeValue(clanElement, "name");
                    config.ClanTag = GetAttributeValue(clanElement, "tag");
                }

                var availableAmmoElement = shipElement.Element("AvailableAmmo");
                if (availableAmmoElement != null)
                {
                    foreach (var ammoElement in availableAmmoElement.Elements("Ammo"))
                    {
                        var id = GetAttributeValue(ammoElement, "id");
                        var amount = GetAttributeIntValue(ammoElement, "amount", 0);
                        if (!string.IsNullOrWhiteSpace(id))
                            config.AvailableAmmo[id] = amount;
                    }
                }

                var specialAmmoElement = shipElement.Element("SpecialAmmo");
                if (specialAmmoElement != null)
                {
                    config.EmpAmount = GetAttributeIntValue(specialAmmoElement, "emp", 0);
                    config.IshAmount = GetAttributeIntValue(specialAmmoElement, "ist", GetAttributeIntValue(specialAmmoElement, "ish", 0));
                    config.SmbAmount = GetAttributeIntValue(specialAmmoElement, "smb", 0);
                }

                result.Add(config);
            }

            return result;
        }

        private static void SpawnConfiguredFakeShip(FakeShipConfig config)
        {
            if (config == null)
                return;

            var ship = GameManager.GetShip(config.ShipId);
            if (ship == null)
                return;

            var baseMapId = GetBaseMapIdByFaction(config.FactionId);
            var map = GameManager.GetSpacemap(baseMapId);
            if (map == null)
                return;

            var basePosition = GetBasePositionByFaction(config.FactionId);
            var clan = ResolveClan(config.ClanName, config.ClanTag, config.FactionId);

            CreateConfiguredFakePlayer(
                ship,
                map,
                basePosition,
                config.FactionId,
                config.Name,
                clan,
                config.RankId,
                config.Level,
                config.Ability,
                config.Objective,
                config.AvailableAmmo,
                config.EmpAmount,
                config.IshAmount,
                config.SmbAmount,
                config.Drones);
        }

        private static List<Drones> ReadDrones(XElement dronesElement)
        {
            var result = new List<Drones>();
            if (dronesElement == null)
                return result;

            var nextId = 1;
            foreach (var droneElement in dronesElement.Elements("Drone"))
            {
                var droneType = GetAttributeIntValue(droneElement, "type", 2);
                var level = GetAttributeIntValue(droneElement, "level", 1);
                var damage = GetAttributeIntValue(droneElement, "damage", 0);
                result.Add(new Drones(nextId++, (byte)droneType, 0, damage, level));
            }

            return result;
        }

        private static Clan ResolveClan(string clanName, string clanTag, int factionId)
        {
            var normalizedName = (clanName ?? "").Trim();
            var normalizedTag = (clanTag ?? "").Trim();

            if (normalizedName.Length == 0 && normalizedTag.Length == 0)
                return GameManager.GetClan(0);

            var existing = GameManager.Clans.Values.FirstOrDefault(x =>
                x != null &&
                string.Equals((x.Name ?? "").Trim(), normalizedName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((x.Tag ?? "").Trim(), normalizedTag, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            var clan = new Clan(nextDynamicClanId--, normalizedName, normalizedTag, factionId);
            GameManager.Clans.TryAdd(clan.Id, clan);
            return clan;
        }

        private static int GetBaseMapIdByFaction(int factionId)
        {
            return factionId == 2 ? 5 : factionId == 3 ? 9 : 1;
        }

        private static Position GetBasePositionByFaction(int factionId)
        {
            if (factionId == 2)
                return Position.EICPosition;
            if (factionId == 3)
                return Position.VRUPosition;
            return Position.MMOPosition;
        }

        private static string NormalizeObjective(string objective)
        {
            var normalized = (objective ?? "").Trim().ToLowerInvariant();
            if (normalized == "idle" || normalized == "parado")
                return "idle";
            if (normalized == "patrol" || normalized == "patrol_base" || normalized == "patrulha")
                return "patrol";
            return "hunt_npcs";
        }

        private static string GetValue(XElement parent, string elementName, string fallback = "")
        {
            var value = parent?.Element(elementName)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int GetIntValue(XElement parent, string elementName, int fallback)
        {
            int value;
            return int.TryParse(GetValue(parent, elementName), out value) ? value : fallback;
        }

        private static string GetAttributeValue(XElement element, string attributeName, string fallback = "")
        {
            var value = element?.Attribute(attributeName)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int GetAttributeIntValue(XElement element, string attributeName, int fallback)
        {
            int value;
            return int.TryParse(GetAttributeValue(element, attributeName), out value) ? value : fallback;
        }

        private static void EnsureConfigFileExists(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(path))
                    return;

                File.WriteAllText(path, GetDefaultConfigXml());
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [AIShips.cs] EnsureConfigFileExists exception: {e}");
            }
        }

        private static string GetDefaultConfigXml()
        {
            return
@"<?xml version=""1.0"" encoding=""utf-8""?>
<FakeShips>
  <FakeShip>
    <Name>Sentinel MMO</Name>
    <Clan name=""Guardians"" tag=""GDN"" />
    <Faction>1</Faction>
    <ShipId>10</ShipId>
    <Rank>5</Rank>
    <Level>16</Level>
    <Ability>none</Ability>
    <Objective>hunt_npcs</Objective>
    <AvailableAmmo>
      <Ammo id=""lcb-10"" amount=""500000"" />
      <Ammo id=""mcb-25"" amount=""250000"" />
      <Ammo id=""mcb-50"" amount=""250000"" />
      <Ammo id=""ucb-100"" amount=""100000"" />
      <Ammo id=""r-310"" amount=""50000"" />
    </AvailableAmmo>
    <SpecialAmmo emp=""10"" ist=""10"" smb=""10"" />
    <Drones>
      <Drone type=""2"" level=""6"" damage=""0"" />
      <Drone type=""2"" level=""6"" damage=""0"" />
      <Drone type=""2"" level=""6"" damage=""0"" />
      <Drone type=""2"" level=""6"" damage=""0"" />
      <Drone type=""2"" level=""6"" damage=""0"" />
      <Drone type=""2"" level=""6"" damage=""0"" />
      <Drone type=""2"" level=""6"" damage=""0"" />
      <Drone type=""2"" level=""6"" damage=""0"" />
    </Drones>
  </FakeShip>
</FakeShips>";
        }
    }
}
