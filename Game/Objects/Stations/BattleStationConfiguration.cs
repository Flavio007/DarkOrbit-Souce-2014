using Ow.Game.Movements;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Ow.Game.Objects.Stations
{
    class BattleStationLevelDefinition
    {
        public int Level { get; set; }
        public int MaxHitPoints { get; set; }
        public int MaxShieldPoints { get; set; }
        public int Damage { get; set; }
        public int Range { get; set; }
        public double CooldownSeconds { get; set; }
        public double MissProbability { get; set; }
        public int RepairAmount { get; set; }
        public double RepairIntervalSeconds { get; set; }
        public int BoostPercent { get; set; }
    }

    class BattleStationWindowDefinition
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        public bool Contains(DateTime timestamp)
        {
            var timeOfDay = timestamp.TimeOfDay;

            if (Start == End)
                return false;

            if (Start < End)
                return timeOfDay >= Start && timeOfDay < End;

            return timeOfDay >= Start || timeOfDay < End;
        }

        public DateTime GetNextStart(DateTime timestamp)
        {
            var candidate = timestamp.Date.Add(Start);
            if (candidate <= timestamp)
                candidate = candidate.AddDays(1);
            return candidate;
        }

        public DateTime GetNextEnd(DateTime timestamp)
        {
            var candidate = timestamp.Date.Add(End);
            if (Start > End)
            {
                var inWrappedWindow = timestamp.TimeOfDay < End || timestamp.TimeOfDay >= Start;
                if (timestamp.TimeOfDay >= Start)
                    candidate = candidate.AddDays(1);
                else if (!inWrappedWindow && candidate <= timestamp)
                    candidate = candidate.AddDays(1);
            }
            else if (candidate <= timestamp)
            {
                candidate = candidate.AddDays(1);
            }

            return candidate;
        }
    }

    class BattleStationTowerDefinition
    {
        public string Name { get; set; }
        public int SlotId { get; set; }
        public short Type { get; set; }
        public short AssetTypeId { get; set; }
        public int DesignId { get; set; }
        public List<BattleStationLevelDefinition> Levels { get; set; }

        public BattleStationLevelDefinition GetLevelDefinition(int level)
        {
            if (Levels == null || Levels.Count == 0)
                return new BattleStationLevelDefinition { Level = 1, MaxHitPoints = 90000, MaxShieldPoints = 90000, Damage = 1000, Range = 590, CooldownSeconds = 1, MissProbability = 0.1, RepairIntervalSeconds = 10 };

            return Levels
                .OrderBy(x => x.Level)
                .LastOrDefault(x => x.Level <= level) ?? Levels.OrderBy(x => x.Level).First();
        }
    }

    class BattleStationDefinition
    {
        public int MapId { get; set; }
        public string Name { get; set; }
        public Position Position { get; set; }
        public short StationAssetTypeId { get; set; }
        public short AsteroidAssetTypeId { get; set; }
        public int CaptureRadius { get; set; }
        public int CaptureSeconds { get; set; }
        public int MinPlayersToCapture { get; set; }
        public int MaxHitPoints { get; set; }
        public int MaxShieldPoints { get; set; }
        public List<BattleStationLevelDefinition> CenterLevels { get; set; }
        public List<BattleStationWindowDefinition> VulnerabilityWindows { get; set; }
        public List<BattleStationTowerDefinition> Towers { get; set; }

        public BattleStationLevelDefinition GetCenterLevelDefinition(int level)
        {
            if (CenterLevels == null || CenterLevels.Count == 0)
                return new BattleStationLevelDefinition { Level = 1, MaxHitPoints = MaxHitPoints, MaxShieldPoints = MaxShieldPoints };

            return CenterLevels
                .OrderBy(x => x.Level)
                .LastOrDefault(x => x.Level <= level) ?? CenterLevels.OrderBy(x => x.Level).First();
        }

        public int GetMaxLevel()
        {
            return CenterLevels != null && CenterLevels.Count > 0 ? CenterLevels.Max(x => x.Level) : 1;
        }

        public bool IsVulnerableAt(DateTime timestamp)
        {
            return VulnerabilityWindows != null && VulnerabilityWindows.Any(window => window.Contains(timestamp));
        }

        public int GetSecondsUntilStateChange(DateTime timestamp)
        {
            if (VulnerabilityWindows == null || VulnerabilityWindows.Count == 0)
                return 0;

            var vulnerable = IsVulnerableAt(timestamp);
            DateTime nextTransition;

            if (vulnerable)
                nextTransition = VulnerabilityWindows.Where(window => window.Contains(timestamp)).Min(window => window.GetNextEnd(timestamp));
            else
                nextTransition = VulnerabilityWindows.Min(window => window.GetNextStart(timestamp));

            var remaining = (int)Math.Ceiling((nextTransition - timestamp).TotalSeconds);
            return remaining < 0 ? 0 : remaining;
        }
    }

    static class BattleStationConfiguration
    {
        private const string DefaultConfigRelativePath = "config\\battle_stations.xml";

        private static readonly object SyncRoot = new object();
        private static List<BattleStationDefinition> cachedDefinitions;

        public static void SpawnConfiguredStations()
        {
            foreach (var definition in LoadDefinitions())
            {
                var map = GameManager.GetSpacemap(definition.MapId);
                if (map == null)
                    continue;

                if (GameManager.BattleStations.ContainsKey(definition.Name))
                    continue;

                var battleStation = new BattleStation(definition, map);
                GameManager.BattleStations.TryAdd(definition.Name, battleStation);
            }
        }

        private static List<BattleStationDefinition> LoadDefinitions()
        {
            lock (SyncRoot)
            {
                if (cachedDefinitions != null)
                    return cachedDefinitions;

                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultConfigRelativePath);
                EnsureConfigFileExists(path);

                if (!File.Exists(path))
                {
                    cachedDefinitions = new List<BattleStationDefinition>();
                    return cachedDefinitions;
                }

                try
                {
                    var document = XDocument.Load(path);
                    cachedDefinitions = ParseDefinitions(document);
                }
                catch (Exception exception)
                {
                    Logger.Log("error_log", $"- [BattleStationConfiguration.cs] Failed to load battle station config: {exception}");
                    cachedDefinitions = new List<BattleStationDefinition>();
                }

                return cachedDefinitions;
            }
        }

        private static void EnsureConfigFileExists(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(path))
                return;

            File.WriteAllText(path,
@"<?xml version=""1.0"" encoding=""utf-8""?>
<BattleStations>
    <Defaults stationAssetTypeId=""36"" asteroidAssetTypeId=""35"" captureRadius=""450"" captureSeconds=""10"" minPlayersToCapture=""1"" maxHitPoints=""250000"" maxShieldPoints=""250000"" towerAssetTypeId=""37"">
        <CenterLevels>
            <Level level=""1"" hitPoints=""250000"" shieldPoints=""250000"" />
            <Level level=""2"" hitPoints=""325000"" shieldPoints=""325000"" />
            <Level level=""3"" hitPoints=""400000"" shieldPoints=""400000"" />
        </CenterLevels>
    </Defaults>
  <BattleStation mapId=""16"" name=""4-4 Battle Station"" x=""20900"" y=""13000"">
    <Vulnerability start=""12:00"" end=""12:30"" />
    <Vulnerability start=""20:00"" end=""20:30"" />
        <Tower slotId=""2"" type=""LASER_LOW_RANGE"" designId=""6"" name=""North-East Laser Tower""><Level level=""1"" hitPoints=""90000"" shieldPoints=""90000"" damage=""1000"" range=""590"" cooldownSeconds=""1.0"" missProbability=""0.10"" /><Level level=""2"" hitPoints=""120000"" shieldPoints=""120000"" damage=""1400"" range=""610"" cooldownSeconds=""0.9"" missProbability=""0.08"" /><Level level=""3"" hitPoints=""150000"" shieldPoints=""150000"" damage=""1800"" range=""630"" cooldownSeconds=""0.8"" missProbability=""0.06"" /></Tower>
        <Tower slotId=""3"" type=""HONOR_BOOSTER"" designId=""9"" name=""East Honour Booster""><Level level=""1"" hitPoints=""90000"" shieldPoints=""90000"" boostPercent=""5"" /><Level level=""2"" hitPoints=""120000"" shieldPoints=""120000"" boostPercent=""10"" /><Level level=""3"" hitPoints=""150000"" shieldPoints=""150000"" boostPercent=""15"" /></Tower>
        <Tower slotId=""4"" type=""LASER_LOW_RANGE"" designId=""6"" name=""South-East Laser Tower""><Level level=""1"" hitPoints=""90000"" shieldPoints=""90000"" damage=""1000"" range=""590"" cooldownSeconds=""1.0"" missProbability=""0.10"" /><Level level=""2"" hitPoints=""120000"" shieldPoints=""120000"" damage=""1400"" range=""610"" cooldownSeconds=""0.9"" missProbability=""0.08"" /><Level level=""3"" hitPoints=""150000"" shieldPoints=""150000"" damage=""1800"" range=""630"" cooldownSeconds=""0.8"" missProbability=""0.06"" /></Tower>
        <Tower slotId=""5"" type=""EXPERIENCE_BOOSTER"" designId=""11"" name=""South Experience Booster""><Level level=""1"" hitPoints=""90000"" shieldPoints=""90000"" boostPercent=""5"" /><Level level=""2"" hitPoints=""120000"" shieldPoints=""120000"" boostPercent=""10"" /><Level level=""3"" hitPoints=""150000"" shieldPoints=""150000"" boostPercent=""15"" /></Tower>
        <Tower slotId=""6"" type=""LASER_LOW_RANGE"" designId=""6"" name=""South-West Laser Tower""><Level level=""1"" hitPoints=""90000"" shieldPoints=""90000"" damage=""1000"" range=""590"" cooldownSeconds=""1.0"" missProbability=""0.10"" /><Level level=""2"" hitPoints=""120000"" shieldPoints=""120000"" damage=""1400"" range=""610"" cooldownSeconds=""0.9"" missProbability=""0.08"" /><Level level=""3"" hitPoints=""150000"" shieldPoints=""150000"" damage=""1800"" range=""630"" cooldownSeconds=""0.8"" missProbability=""0.06"" /></Tower>
          <Tower slotId=""7"" type=""REPAIR"" designId=""3"" name=""West Repair Tower""><Level level=""1"" hitPoints=""90000"" shieldPoints=""90000"" repairAmount=""5000"" repairIntervalSeconds=""10"" /><Level level=""2"" hitPoints=""120000"" shieldPoints=""120000"" repairAmount=""7500"" repairIntervalSeconds=""10"" /><Level level=""3"" hitPoints=""150000"" shieldPoints=""150000"" repairAmount=""10000"" repairIntervalSeconds=""10"" /></Tower>
        <Tower slotId=""8"" type=""LASER_LOW_RANGE"" designId=""6"" name=""North-West Laser Tower""><Level level=""1"" hitPoints=""90000"" shieldPoints=""90000"" damage=""1000"" range=""590"" cooldownSeconds=""1.0"" missProbability=""0.10"" /><Level level=""2"" hitPoints=""120000"" shieldPoints=""120000"" damage=""1400"" range=""610"" cooldownSeconds=""0.9"" missProbability=""0.08"" /><Level level=""3"" hitPoints=""150000"" shieldPoints=""150000"" damage=""1800"" range=""630"" cooldownSeconds=""0.8"" missProbability=""0.06"" /></Tower>
        <Tower slotId=""9"" type=""ROCKET_MID_ACCURACY"" designId=""7"" name=""North Missile Tower""><Level level=""1"" hitPoints=""90000"" shieldPoints=""90000"" damage=""1400"" range=""780"" cooldownSeconds=""2.0"" missProbability=""0.30"" /><Level level=""2"" hitPoints=""120000"" shieldPoints=""120000"" damage=""1900"" range=""820"" cooldownSeconds=""1.8"" missProbability=""0.24"" /><Level level=""3"" hitPoints=""150000"" shieldPoints=""150000"" damage=""2400"" range=""860"" cooldownSeconds=""1.6"" missProbability=""0.18"" /></Tower>
  </BattleStation>
</BattleStations>");
        }

        private static List<BattleStationDefinition> ParseDefinitions(XDocument document)
        {
            var definitions = new List<BattleStationDefinition>();
            var root = document.Element("BattleStations");
            if (root == null)
                return definitions;

            var defaults = root.Element("Defaults");

            foreach (var stationElement in root.Elements("BattleStation"))
            {
                int mapId;
                int x;
                int y;

                if (!TryGetInt(stationElement, "mapId", out mapId) || !TryGetInt(stationElement, "x", out x) || !TryGetInt(stationElement, "y", out y))
                    continue;

                var definition = new BattleStationDefinition
                {
                    MapId = mapId,
                    Name = GetAttributeValue(stationElement, "name", $"battle_station_{mapId}_{x}_{y}"),
                    Position = new Position(x, y),
                    StationAssetTypeId = GetShortAttribute(stationElement, defaults, "stationAssetTypeId", AssetTypeModule.BATTLESTATION),
                    AsteroidAssetTypeId = GetShortAttribute(stationElement, defaults, "asteroidAssetTypeId", AssetTypeModule.ASTEROID),
                    CaptureRadius = GetIntAttribute(stationElement, defaults, "captureRadius", 450),
                    CaptureSeconds = GetIntAttribute(stationElement, defaults, "captureSeconds", 10),
                    MinPlayersToCapture = GetIntAttribute(stationElement, defaults, "minPlayersToCapture", 1),
                    MaxHitPoints = GetIntAttribute(stationElement, defaults, "maxHitPoints", 250000),
                    MaxShieldPoints = GetIntAttribute(stationElement, defaults, "maxShieldPoints", 250000),
                    CenterLevels = ParseCenterLevels(stationElement, defaults),
                    VulnerabilityWindows = ParseWindows(stationElement),
                    Towers = ParseTowers(stationElement, defaults)
                };

                definitions.Add(definition);
            }

            return definitions;
        }

        private static List<BattleStationWindowDefinition> ParseWindows(XElement stationElement)
        {
            var windows = new List<BattleStationWindowDefinition>();

            foreach (var windowElement in stationElement.Elements("Vulnerability"))
            {
                TimeSpan start;
                TimeSpan end;

                if (!TimeSpan.TryParseExact(GetAttributeValue(windowElement, "start"), @"hh\:mm", CultureInfo.InvariantCulture, out start))
                    continue;
                if (!TimeSpan.TryParseExact(GetAttributeValue(windowElement, "end"), @"hh\:mm", CultureInfo.InvariantCulture, out end))
                    continue;

                windows.Add(new BattleStationWindowDefinition { Start = start, End = end });
            }

            return windows;
        }

        private static List<BattleStationLevelDefinition> ParseCenterLevels(XElement stationElement, XElement defaults)
        {
            var centerLevels = new List<BattleStationLevelDefinition>();
            var levelsContainer = stationElement.Element("CenterLevels") ?? defaults?.Element("CenterLevels");

            if (levelsContainer != null)
            {
                foreach (var levelElement in levelsContainer.Elements("Level"))
                {
                    var levelDefinition = ParseLevelDefinition(levelElement);
                    if (levelDefinition != null)
                        centerLevels.Add(levelDefinition);
                }
            }

            if (centerLevels.Count == 0)
            {
                centerLevels.Add(new BattleStationLevelDefinition
                {
                    Level = 1,
                    MaxHitPoints = GetIntAttribute(stationElement, defaults, "maxHitPoints", 250000),
                    MaxShieldPoints = GetIntAttribute(stationElement, defaults, "maxShieldPoints", 250000)
                });
            }

            return centerLevels.OrderBy(x => x.Level).ToList();
        }

        private static List<BattleStationTowerDefinition> ParseTowers(XElement stationElement, XElement defaults)
        {
            var towers = new List<BattleStationTowerDefinition>();

            foreach (var towerElement in stationElement.Elements("Tower"))
            {
                int slotId;
                if (!TryGetInt(towerElement, "slotId", out slotId))
                    continue;

                towers.Add(new BattleStationTowerDefinition
                {
                    SlotId = slotId,
                    Name = GetAttributeValue(towerElement, "name", $"Tower {slotId}"),
                    Type = ResolveTowerType(GetAttributeValue(towerElement, "type", "LASER_LOW_RANGE")),
                    AssetTypeId = GetShortAttribute(towerElement, defaults, "towerAssetTypeId", AssetTypeModule.SATELLITE),
                    DesignId = GetIntAttribute(towerElement, defaults, "designId", 6),
                    Levels = ParseTowerLevels(towerElement, defaults)
                });
            }

            return towers;
        }

        private static List<BattleStationLevelDefinition> ParseTowerLevels(XElement towerElement, XElement defaults)
        {
            var levels = new List<BattleStationLevelDefinition>();

            foreach (var levelElement in towerElement.Elements("Level"))
            {
                var levelDefinition = ParseLevelDefinition(levelElement);
                if (levelDefinition != null)
                    levels.Add(levelDefinition);
            }

            if (levels.Count == 0)
            {
                levels.Add(new BattleStationLevelDefinition
                {
                    Level = 1,
                    MaxHitPoints = GetIntAttribute(towerElement, defaults, "towerHitPoints", 90000),
                    MaxShieldPoints = GetIntAttribute(towerElement, defaults, "towerShieldPoints", 90000),
                    Damage = GetIntAttribute(towerElement, defaults, "damage", 1000),
                    Range = GetIntAttribute(towerElement, defaults, "range", 590),
                    CooldownSeconds = GetDoubleAttribute(towerElement, defaults, "cooldownSeconds", 1),
                    MissProbability = GetDoubleAttribute(towerElement, defaults, "missProbability", 0.10),
                    RepairAmount = GetIntAttribute(towerElement, defaults, "repairAmount", 2500),
                    RepairIntervalSeconds = GetDoubleAttribute(towerElement, defaults, "repairIntervalSeconds", 10),
                    BoostPercent = GetIntAttribute(towerElement, defaults, "boostPercent", 5)
                });
            }

            return levels.OrderBy(x => x.Level).ToList();
        }

        private static BattleStationLevelDefinition ParseLevelDefinition(XElement levelElement)
        {
            int level;
            if (!TryGetInt(levelElement, "level", out level))
                return null;

            return new BattleStationLevelDefinition
            {
                Level = level,
                MaxHitPoints = GetIntAttribute(levelElement, null, "hitPoints", 0),
                MaxShieldPoints = GetIntAttribute(levelElement, null, "shieldPoints", 0),
                Damage = GetIntAttribute(levelElement, null, "damage", 0),
                Range = GetIntAttribute(levelElement, null, "range", 0),
                CooldownSeconds = GetDoubleAttribute(levelElement, null, "cooldownSeconds", 0),
                MissProbability = GetDoubleAttribute(levelElement, null, "missProbability", 0),
                RepairAmount = GetIntAttribute(levelElement, null, "repairAmount", 0),
                RepairIntervalSeconds = GetDoubleAttribute(levelElement, null, "repairIntervalSeconds", 0),
                BoostPercent = GetIntAttribute(levelElement, null, "boostPercent", 0)
            };
        }

        private static short ResolveTowerType(string rawType)
        {
            switch ((rawType ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "LASER_HIGH_RANGE":
                    return StationModuleModule.LASER_HIGH_RANGE;
                case "LASER_MID_RANGE":
                    return StationModuleModule.LASER_MID_RANGE;
                case "ROCKET_MID_ACCURACY":
                    return StationModuleModule.ROCKET_MID_ACCURACY;
                case "ROCKET_LOW_ACCURACY":
                    return StationModuleModule.ROCKET_LOW_ACCURACY;
                case "REPAIR":
                    return StationModuleModule.REPAIR;
                case "HONOR_BOOSTER":
                    return StationModuleModule.HONOR_BOOSTER;
                case "EXPERIENCE_BOOSTER":
                    return StationModuleModule.EXPERIENCE_BOOSTER;
                case "DAMAGE_BOOSTER":
                    return StationModuleModule.DAMAGE_BOOSTER;
                default:
                    return StationModuleModule.LASER_LOW_RANGE;
            }
        }

        private static bool TryGetInt(XElement element, string attributeName, out int value)
        {
            return int.TryParse(GetAttributeValue(element, attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static int GetIntAttribute(XElement element, XElement defaults, string attributeName, int fallback)
        {
            int value;
            if (int.TryParse(GetAttributeValue(element, attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;

            if (defaults != null && int.TryParse(GetAttributeValue(defaults, attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;

            return fallback;
        }

        private static double GetDoubleAttribute(XElement element, XElement defaults, string attributeName, double fallback)
        {
            double value;
            if (double.TryParse(GetAttributeValue(element, attributeName), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
                return value;

            if (defaults != null && double.TryParse(GetAttributeValue(defaults, attributeName), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
                return value;

            return fallback;
        }

        private static short GetShortAttribute(XElement element, XElement defaults, string attributeName, short fallback)
        {
            short value;
            if (short.TryParse(GetAttributeValue(element, attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;

            if (defaults != null && short.TryParse(GetAttributeValue(defaults, attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;

            return fallback;
        }

        private static string GetAttributeValue(XElement element, string attributeName, string fallback = "")
        {
            var attribute = element.Attribute(attributeName);
            return attribute == null ? fallback : attribute.Value;
        }
    }
}