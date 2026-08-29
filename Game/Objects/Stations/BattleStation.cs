using Ow.Game;
using Ow.Game.Movements;
using Ow.Game.Objects;
using Ow.Managers;
using Ow.Net.netty;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Game.Objects.Stations
{
    public class EquippedModuleBase
    {
        public int ClanId { get; set; }
        public List<SatelliteBase> Modules { get; set; }

        public EquippedModuleBase(int clanId, List<SatelliteBase> satellites)
        {
            ClanId = clanId;
            Modules = satellites;
        }
    }

    class BattleStation : Activatable
    {
        private const int DefaultClanStationHitPoints = 250000;
        private const int DefaultClanStationShieldPoints = 250000;
        private const int DefaultClanBuildRange = 700;
        private const int ClanOffMapBoostPercentage = 50;
        private const int ClanModuleInstallationSeconds = 60;

        public Dictionary<int, List<Satellite>> EquippedStationModule = new Dictionary<int, List<Satellite>>();

        public bool InBuildingState = false;
        public int BuildTimeInMinutes = 0;

        public bool DeflectorActive = false;
        public int DeflectorSecondsLeft = 0;
        public int DeflectorSecondsMax = 0;

        public string AsteroidName { get; private set; }
        public BattleStationDefinition Definition { get; private set; }
        public List<Satellite> DefenseTowers { get; private set; }
        public int Level { get; private set; }
        public int UpgradeLevel => Level;

        public DateTime buildTime = DateTime.MinValue;
        public DateTime deflectorTime = DateTime.MinValue;

        private int capturingFactionId;
        private int captureProgressPoints;
        private DateTime lastCaptureProgressAt = DateTime.MinValue;
        private bool previousVulnerabilityState;
        private readonly HashSet<int> sectorControlProgressBarPlayers = new HashSet<int>();
        private readonly object sectorControlProgressBarPlayersLock = new object();
        // Tracks the single SZ registration per player.  The client creates
        // the type-41 beacon every time it receives a MapAddPOICommand.
        private readonly HashSet<int> sectorControlPoiPlayers = new HashSet<int>();
        private readonly HashSet<int> sectorControlProgressBarVisibilityPending = new HashSet<int>();
        private int lastSectorControlProgressPercent = -1;
        private int lastSectorControlCapturingFactionId = -1;
        private string lastSectorControlCapturingFactions = null;

        public bool IsFactionBattleStation => Definition != null;
        public bool IsClanBattleStation => !IsFactionBattleStation;
        public bool IsOwned => IsClanBattleStation ? Clan != null && Clan.Id != 0 : FactionId != 0;
        public bool IsOperational => AssetTypeId == AssetTypeModule.BATTLESTATION;
        public string SectorControlHash { get; private set; }
        private string SectorControlVisualHash => $"{SectorControlHash}_visual";
        private bool IsSectorControlStation => IsFactionBattleStation && Spacemap != null && Spacemap.Id == 16;

        public BattleStation(BattleStationDefinition definition, Spacemap spacemap)
            : base(spacemap, 0, definition.Position, GameManager.GetClan(0), definition.AsteroidAssetTypeId)
        {
            Definition = definition;
            AsteroidName = definition.Name;
            Name = definition.Name;
            DefenseTowers = new List<Satellite>();

            ShieldAbsorption = 0.8;
            Level = 0;
            ApplyLevelStats(true);
            Invincible = true;
            previousVulnerabilityState = Definition.IsVulnerableAt(DateTime.Now);
            SectorControlHash = $"sector_control_{spacemap.Id}_{Id}";
            RegisterSectorControlPOI();
            RegisterSectorControlVisualPOI();

            Program.TickManager.AddTick(this);
        }

        public BattleStation(int id, string name, Spacemap spacemap, Position position, Clan clan, bool active)
            : base(id, spacemap, ResolveClanFactionId(clan, 0), position, clan ?? GameManager.GetClan(0), active ? AssetTypeModule.BATTLESTATION : AssetTypeModule.ASTEROID)
        {
            Definition = null;
            AsteroidName = name;
            Name = name;
            DefenseTowers = new List<Satellite>();

            ShieldAbsorption = 0.8;
            Level = 1;
            ApplyLevelStats(true);
            Invincible = false;
            previousVulnerabilityState = false;

            Program.TickManager.AddTick(this);
        }

        public new void Tick()
        {
            if (IsClanBattleStation)
            {
                ProcessClanBuild();
                return;
            }

            var wasVulnerable = previousVulnerabilityState;
            UpdateShieldState();
            var isVulnerable = IsCurrentlyVulnerable();

            if (FactionId == 0)
                ProcessCapture();
            else
                ResetCaptureProgress();

            if (FactionId == 0)
            {
                EnsureSectorControlBeaconPOI();
                UpdateSectorControlPOI();
            }

            UpdateSectorControlProgressBars();

            if (FactionId != 0 && wasVulnerable && !isVulnerable)
                HandleVulnerabilitySurvived();

            previousVulnerabilityState = isVulnerable;
        }

        public bool IsCurrentlyVulnerable()
        {
            return Definition != null && Definition.IsVulnerableAt(DateTime.Now);
        }

        public static bool CanClanUseMap(Clan clan, Spacemap spacemap)
        {
            if (clan == null || clan.Id == 0 || spacemap == null)
                return false;

            if (clan.FactionId <= 0)
                return true;

            return spacemap.FactionId == 0 || spacemap.FactionId == clan.FactionId;
        }

        public bool CanBeBuiltBy(Player player)
        {
            if (player == null || !IsClanBattleStation || IsOperational || InBuildingState)
                return false;

            if (player.Clan == null || player.Clan.Id == 0)
                return false;

            if (Clan != null && Clan.Id != 0 && Clan.Id != player.Clan.Id)
                return false;

            if (!CanClanUseMap(player.Clan, player.Spacemap))
                return false;

            if (!HasRequiredCoreModules())
                return false;

            return player.Position.DistanceTo(Position) <= DefaultClanBuildRange;
        }

        public bool CanManage(Player player)
        {
            if (player == null || !IsClanBattleStation || player.Clan == null)
                return false;

            var managingClan = Clan != null && Clan.Id != 0 ? Clan : player.Clan;

            if (managingClan == null || managingClan.Id == 0)
                return false;

            if (Clan != null && Clan.Id != 0 && player.Clan.Id != Clan.Id)
                return false;

            return managingClan.LeaderId > 0
                && player.Id == managingClan.LeaderId
                && CanClanUseMap(player.Clan, player.Spacemap);
        }

        public bool IsFriendlyTo(Attackable target)
        {
            if (target == null)
                return false;

            if (IsFactionBattleStation)
                return FactionId != 0 && target.FactionId == FactionId;

            if (Clan == null || Clan.Id == 0 || target.Clan == null || target.Clan.Id == 0)
                return false;

            if (target.Clan.Id == Clan.Id)
                return true;

            var relation = Clan.GetRelation(target.Clan);
            return relation == (short)Diplomacy.ALLIED || relation == (short)Diplomacy.NON_AGGRESSION_PACT;
        }

        public IEnumerable<Satellite> GetSatellites()
        {
            var staticSatellites = DefenseTowers ?? Enumerable.Empty<Satellite>();
            var equippedSatellites = EquippedStationModule.Values.SelectMany(x => x ?? new List<Satellite>());
            return staticSatellites.Concat(equippedSatellites).Where(x => x != null);
        }

        public Satellite GetSatelliteByItemId(int itemId)
        {
            return GetSatellites().FirstOrDefault(x => x.ItemId == itemId);
        }

        public Satellite GetSatelliteBySlotId(int slotId)
        {
            return GetSatellites().FirstOrDefault(x => x.SlotId == slotId && !x.IsDestroyedModuleState);
        }

        public bool IsValidModuleSlot(short moduleType, int slotId)
        {
            if (!IsClanBattleStation)
                return false;

            if (moduleType == StationModuleModule.HULL)
                return slotId == 0;

            if (moduleType == StationModuleModule.DEFLECTOR)
                return slotId == 1;

            return slotId >= 2 && slotId <= 9;
        }

        public bool ShouldDisplayModuleAsSatellite(int slotId)
        {
            return IsOperational && slotId >= 2;
        }

        public int GetDefaultModuleDesignId(short type)
        {
            switch (type)
            {
                case StationModuleModule.REPAIR:
                    return 3;
                case StationModuleModule.LASER_HIGH_RANGE:
                    return 4;
                case StationModuleModule.LASER_MID_RANGE:
                    return 5;
                case StationModuleModule.LASER_LOW_RANGE:
                    return 6;
                case StationModuleModule.ROCKET_MID_ACCURACY:
                    return 7;
                case StationModuleModule.ROCKET_LOW_ACCURACY:
                    return 8;
                case StationModuleModule.HONOR_BOOSTER:
                    return 9;
                case StationModuleModule.DAMAGE_BOOSTER:
                    return 10;
                case StationModuleModule.EXPERIENCE_BOOSTER:
                    return 11;
                default:
                    return 6;
            }
        }

        private int GetClanModuleDesignId(short type, int persistedDesignId = 0)
        {
            if (!IsClanBattleStation)
                return persistedDesignId > 0 ? persistedDesignId : GetDefaultModuleDesignId(type);

            return GetDefaultModuleDesignId(type);
        }

        public AvailableModulesCommand GetAvailableModulesCommand(Clan clan)
        {
            if (clan == null || clan.Id == 0)
                return new AvailableModulesCommand(new List<StationModuleModule>());

            var modules = clan.BattleStationInventory
                .Where(module => module != null && !module.InUse)
                .Select(module => new StationModuleModule(
                    Id,
                    module.ItemId,
                    0,
                    module.Type,
                    0,
                    0,
                    0,
                    0,
                    module.UpgradeLevel,
                    GetClanModuleDisplayLabel(module.UpgradeLevel),
                    0,
                    0,
                    0,
                    0,
                    0))
                .ToList();

            return new AvailableModulesCommand(modules);
        }

        public bool TryResolveClanModule(int itemId, Clan clan, out ClanBattleStationInventoryItem module)
        {
            module = null;

            if (clan == null || clan.Id == 0)
                return false;

            module = clan.BattleStationInventory.FirstOrDefault(candidate => candidate.ItemId == itemId);
            return module != null;
        }

        public void UpdateClanModuleUsage(int itemId, bool inUse)
        {
            UpdateClanModuleUsage(itemId, inUse, null);
        }

        public void UpdateClanModuleUsage(int itemId, bool inUse, Clan fallbackClan)
        {
            var moduleClan = ResolveModuleClan(itemId, fallbackClan);
            if (moduleClan == null || moduleClan.Id == 0)
                return;

            var module = moduleClan.BattleStationInventory.FirstOrDefault(x => x.ItemId == itemId);
            if (module == null)
                return;

            module.InUse = inUse;

            QueryManager.SaveClanBattleStationInventory(moduleClan);

            if (IsClanBattleStation)
                RecalculateClanLevel(false);
        }

        private Clan ResolveModuleClan(int itemId, Clan fallbackClan)
        {
            if (Clan != null && Clan.Id != 0)
                return Clan;

            if (fallbackClan != null && fallbackClan.Id != 0)
                return fallbackClan;

            foreach (var equippedModules in EquippedStationModule)
            {
                if (equippedModules.Key <= 0 || equippedModules.Value == null)
                    continue;

                if (equippedModules.Value.Any(module => module != null && module.ItemId == itemId))
                    return GameManager.GetClan(equippedModules.Key);
            }

            return null;
        }

        public void QueueClanBuild(Clan clan, int fallbackFactionId, int buildMinutes)
        {
            if (!IsClanBattleStation)
                return;

            Clan = clan ?? GameManager.GetClan(0);
            FactionId = ResolveClanFactionId(Clan, fallbackFactionId);
            BuildTimeInMinutes = Math.Max(0, buildMinutes);
            buildTime = DateTime.Now.AddMinutes(BuildTimeInMinutes);
            InBuildingState = BuildTimeInMinutes > 0;
            AssetTypeId = InBuildingState ? AssetTypeModule.ASTEROID : AssetTypeModule.BATTLESTATION;
            Level = Math.Max(1, Level);
            ApplyLevelStats(true);
            Invincible = InBuildingState;
            SyncClanConstructionVisual();

            if (InBuildingState)
                RefreshVisual();
            else
                ActivateClanStation();
        }

        public void LoadClanState(bool active, bool inBuildingState, int buildMinutes, DateTime buildEndTime, bool deflectorActive, int deflectorSecondsLeft, DateTime deflectorEndTime)
        {
            if (!IsClanBattleStation)
                return;

            InBuildingState = inBuildingState;
            BuildTimeInMinutes = buildMinutes;
            buildTime = buildEndTime;
            DeflectorActive = deflectorActive;
            DeflectorSecondsLeft = deflectorSecondsLeft;
            DeflectorSecondsMax = deflectorSecondsLeft;
            deflectorTime = deflectorEndTime;
            AssetTypeId = active ? AssetTypeModule.BATTLESTATION : AssetTypeModule.ASTEROID;
            Invincible = inBuildingState || deflectorActive;
            SyncClanConstructionVisual();
        }

        public int GetClanModuleInstallationSeconds()
        {
            return ClanModuleInstallationSeconds;
        }

        public void SendAvailableModulesCommand(Player player)
        {
            if (player?.Clan == null)
                return;

            player.SendCommand(GetAvailableModulesCommand(player.Clan).write());
        }

        private void SyncVisibleClanModules()
        {
            if (!IsClanBattleStation || Spacemap == null)
                return;

            foreach (var satellite in EquippedStationModule.Values.SelectMany(x => x ?? new List<Satellite>()).Where(x => x != null))
            {
                var shouldDisplay = ShouldDisplayModuleAsSatellite(satellite.SlotId);
                var isDisplayed = Spacemap.Activatables.ContainsKey(satellite.Id);

                if (shouldDisplay && !isDisplayed)
                {
                    Spacemap.Activatables.TryAdd(satellite.Id, satellite);
                    GameManager.SendCommandToMap(Spacemap.Id, satellite.GetAssetCreateCommand());
                }
                else if (!shouldDisplay && isDisplayed)
                {
                    Spacemap.Activatables.TryRemove(satellite.Id, out var removedSatellite);
                    GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(satellite.GetAssetType(), satellite.Id));
                }
            }
        }

        public void SendClanInterfaceCommand(Player player)
        {
            if (player?.Clan == null || player.Clan.Id == 0 || !IsClanBattleStation)
                return;

            if (InBuildingState)
            {
                var canManage = CanManage(player);

                player.SendCommand(BattleStationBuildingStateCommand.write(
                    Id,
                    Id,
                    Name,
                    GetSecondsUntilBuildComplete(),
                    Math.Max(0, BuildTimeInMinutes * 60),
                    Clan?.Name ?? player.Clan.Name,
                    new FactionModule((short)GetAffiliatedFactionId())));

                if (canManage)
                {
                    player.SendCommand(BattleStationBuildingUiInitializationCommand.write(
                        Id,
                        Id,
                        Name,
                        new AsteroidProgressCommand(Id, 0, 0, player.Clan.Name, Clan?.Name ?? "", new EquippedModulesModule(GetStatusModules()), false),
                        GetAvailableModulesCommand(player.Clan),
                        1,
                        60,
                        1));
                }

                return;
            }

            if (!IsOperational)
            {
                player.SendCommand(BattleStationBuildingUiInitializationCommand.write(
                    Id,
                    Id,
                    Name,
                    new AsteroidProgressCommand(Id, 0, 0, player.Clan.Name, Clan?.Name ?? "", new EquippedModulesModule(GetStatusModules()), CanBeBuiltBy(player)),
                    GetAvailableModulesCommand(player.Clan),
                    1,
                    60,
                    1));
                return;
            }

            var statusCommand = GetStatusCommand();
            player.SendCommand(statusCommand.writeCommand());

            if (CanManage(player))
            {
                player.SendCommand(BattleStationManagementUiInitializationCommand.write(
                    Id,
                    Id,
                    Name,
                    Clan?.Name ?? "",
                    new FactionModule((short)GetAffiliatedFactionId()),
                    statusCommand,
                    GetAvailableModulesCommand(player.Clan),
                    0,
                    0,
                    0,
                    false));
            }
        }

        public void LoadEquippedModules(IEnumerable<EquippedModuleBase> equippedModules)
        {
            if (!IsClanBattleStation || equippedModules == null)
                return;

            foreach (var equippedModule in equippedModules.Where(x => x != null))
            {
                if (!EquippedStationModule.ContainsKey(equippedModule.ClanId))
                    EquippedStationModule[equippedModule.ClanId] = new List<Satellite>();

                foreach (var moduleBase in equippedModule.Modules.Where(x => x != null))
                {
                    var satellite = new Satellite(
                        this,
                        0,
                        Satellite.GetName(moduleBase.Type),
                        GetClanModuleDesignId(moduleBase.Type, moduleBase.DesignId),
                        moduleBase.ItemId,
                        moduleBase.SlotId,
                        moduleBase.Type,
                        Satellite.GetPosition(Position, moduleBase.SlotId));

                    satellite.CurrentHitPoints = moduleBase.CurrentHitPoints;
                    satellite.MaxHitPoints = moduleBase.MaxHitPoints;
                    satellite.CurrentShieldPoints = moduleBase.CurrentShieldPoints;
                    satellite.MaxShieldPoints = moduleBase.MaxShieldPoints;
                    satellite.InstallationSecondsLeft = moduleBase.InstallationSecondsLeft;
                    satellite.Installed = moduleBase.Installed;
                    satellite.UpgradeLevel = moduleBase.UpgradeLevel;
                    EquippedStationModule[equippedModule.ClanId].Add(satellite);

                    if (ShouldDisplayModuleAsSatellite(moduleBase.SlotId))
                        Spacemap.Activatables.TryAdd(satellite.Id, satellite);
                }
            }

            RecalculateClanLevel(true);
            SyncVisibleClanModules();
        }

        public List<EquippedModuleBase> GetPersistedModules()
        {
            return EquippedStationModule
                .Where(x => x.Value != null && x.Value.Count > 0)
                .Select(x => new EquippedModuleBase(
                    x.Key,
                    x.Value.Where(module => module != null)
                        .Select(module => new SatelliteBase(
                            0,
                            module.ItemId,
                            module.SlotId,
                            GetClanModuleDesignId(module.Type, module.DesignId),
                            module.Type,
                            module.CurrentHitPoints,
                            module.MaxHitPoints,
                            module.CurrentShieldPoints,
                            module.MaxShieldPoints,
                            module.UpgradeLevel,
                            module.InstallationSecondsLeft,
                            module.Installed))
                        .ToList()))
                .ToList();
        }

        public void HandleDestroyed(Attackable destroyer)
        {
            var destroyerName = destroyer != null ? destroyer.Name : "Unknown";
            var ownerName = GetOwnerName();

            GameManager.SendPacketToAll($"0|A|STD|Battle station {AsteroidName} on {Spacemap.Name} was destroyed by {destroyerName}. Previous owner: {ownerName}.");

            Neutralize();
            Destroyed = false;
            QueryManager.BattleStations.BattleStation(this);
            RefreshBoosterInterface();
        }

        public void HandleTowerDestroyed(Satellite tower)
        {
            if (tower == null)
                return;

            tower.Remove();
            DefenseTowers.Remove(tower);
            Spacemap.Activatables.TryRemove(tower.Id, out var removedTower);
            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(tower.GetAssetType(), tower.Id));
            RefreshBoosterInterface();
        }

        public override void Click(GameSession gameSession)
        {
            var player = gameSession.Player;
            if (player == null)
                return;

            if (IsClanBattleStation)
            {
                HandleClanClick(player);
                return;
            }

            if (FactionId == 0)
            {
                var captureState = GetCaptureState();
                var captureText = captureState.Contested
                    ? $"Neutral battle station. Capture progress is paused because multiple factions are within {Definition.CaptureRadius} units. Current progress: {GetFactionName(capturingFactionId)} {captureProgressPoints}/{GetCapturePointsRequired()}."
                    : capturingFactionId == 0 || captureProgressPoints <= 0
                        ? $"Neutral battle station. Each player within {Definition.CaptureRadius} units adds 1 capture point per second for their company, up to {GetMaxCapturePointsPerSecond()} per second. Reach {GetCapturePointsRequired()} points to capture it."
                        : $"Neutral battle station. {GetFactionName(capturingFactionId)} has {captureProgressPoints}/{GetCapturePointsRequired()} capture points.";
                player.SendPacket($"0|A|STD|{captureText}");
                return;
            }

            var shieldState = DeflectorActive ? "Shield active" : "Vulnerable";
            player.SendPacket($"0|A|STD|Battle station owner: {GetFactionName(FactionId)}. {shieldState}. Level {GetEffectiveLevel()} (upgrade {UpgradeLevel}).");
        }

        public override byte[] GetAssetCreateCommand(short clanRelationModule = ClanRelationModule.NONE)
        {
            var ownerClanTag = IsClanBattleStation && Clan != null && Clan.Id != 0 ? Clan.Tag : "";
            var ownerClanId = IsClanBattleStation && Clan != null ? Clan.Id : 0;

            return AssetCreateCommand.write(GetAssetType(), Name,
                GetAffiliatedFactionId(), ownerClanTag, Id, GetVisualDesignId(), GetVisualExpansionStage(),
                Position.X, Position.Y, ownerClanId, true, true, true, true,
                new ClanRelationModule(clanRelationModule),
                VisualModifiers.Values.ToList());
        }

        private void RegisterSectorControlPOI()
        {
            if (!IsSectorControlStation || Definition == null)
                return;

            var radius = Math.Max(1, Definition.CaptureRadius);
            var poi = new POI(
                SectorControlHash,
                POITypes.SECTOR_CONTROL_SECTOR_ZONE,
                POIDesigns.SECTOR_CONTROL_SECTOR_ZONE,
                POIShapes.CIRCLE,
                new List<int> { Position.X, Position.Y, radius },
                true,
                false,
                "NONE");

            // The client maps this numeric POI type to its internal "SZ" module
            // and creates the invisible type-41 beacon automatically.  Keep a
            // single POI/hash so the progress packets resolve that same asset.
            Spacemap.POIs.TryAdd(SectorControlHash, poi);
        }

        private void RegisterSectorControlVisualPOI()
        {
            if (!IsSectorControlStation || Definition == null)
                return;

            var radius = Math.Max(1, Definition.CaptureRadius);
            var poi = new POI(
                SectorControlVisualHash,
                POITypes.SECTOR_CONTROL_HOME_ZONE,
                POIDesigns.SECTOR_CONTROL_SECTOR_ZONE,
                POIShapes.CIRCLE,
                new List<int> { Position.X, Position.Y, radius },
                true,
                false,
                GetSectorControlZoneSpecification());

            // HZ is used only for the colored circle.  Unlike SZ, the client
            // does not create a type-41 beacon when this POI is added.
            Spacemap.POIs.TryAdd(SectorControlVisualHash, poi);
        }

        private void EnsureSectorControlBeaconPOI()
        {
            if (!IsSectorControlStation || FactionId != 0 || Spacemap == null)
                return;

            if (Spacemap.POIs.TryGetValue(SectorControlHash, out var poi) && poi != null)
                return;

            RegisterSectorControlPOI();
            if (!Spacemap.POIs.TryGetValue(SectorControlHash, out poi) || poi == null)
                return;

            var poiCommand = poi.GetPOICreateCommand();
            PacketDebug.Log("sector_control_packets",
                $"QUEUE MapAddPOICommand ID={MapAddPOICommand.ID} LENGTH={poiCommand.Length} PLAYER=MAP MAP={Spacemap.Id} HASH={SectorControlHash} REASON=BEACON_REGISTER");

            lock (sectorControlProgressBarPlayersLock)
            {
                foreach (var player in Spacemap.Characters.Values.OfType<Player>())
                    sectorControlPoiPlayers.Add(player.Id);
            }

            GameManager.SendCommandToMap(Spacemap.Id, poiCommand);
        }

        public void SendSectorControlPOI(Player player, byte[] command, string reason)
        {
            if (player == null || command == null)
                return;

            lock (sectorControlProgressBarPlayersLock)
            {
                if (!sectorControlPoiPlayers.Add(player.Id))
                    return;

                SendSectorControlCommand(
                    player,
                    "MapAddPOICommand",
                    MapAddPOICommand.ID,
                    command,
                    reason);
            }
        }

        private void UpdateSectorControlPOI()
        {
            if (!IsSectorControlStation || FactionId != 0)
                return;

            var created = false;
            if (!Spacemap.POIs.TryGetValue(SectorControlVisualHash, out var poi) || poi == null)
            {
                RegisterSectorControlVisualPOI();
                created = true;
                if (!Spacemap.POIs.TryGetValue(SectorControlVisualHash, out poi) || poi == null)
                    return;
            }

            var zoneSpecification = GetSectorControlZoneSpecification();
            if (!created && poi.TypeSpecification == zoneSpecification)
                return;

            poi.TypeSpecification = zoneSpecification;
            var poiCommand = poi.GetPOICreateCommand();
            PacketDebug.Log("sector_control_packets",
                $"QUEUE MapAddPOICommand ID={MapAddPOICommand.ID} LENGTH={poiCommand.Length} PLAYER=MAP MAP={Spacemap.Id} HASH={SectorControlVisualHash} REASON=VISUAL_ZONE_UPDATE");
            GameManager.SendCommandToMap(Spacemap.Id, poiCommand);
        }

        private void RemoveSectorControlPOI()
        {
            if (!IsSectorControlStation || Spacemap == null)
                return;

            Spacemap.POIs.TryRemove(SectorControlHash, out var removedPoi);
            Spacemap.POIs.TryRemove(SectorControlVisualHash, out var removedVisualPoi);

            lock (sectorControlProgressBarPlayersLock)
            {
                sectorControlPoiPlayers.Clear();
                sectorControlProgressBarVisibilityPending.Clear();
            }

            GameManager.SendCommandToMap(
                Spacemap.Id,
                MapRemovePOICommand.write(SectorControlHash));
            GameManager.SendCommandToMap(
                Spacemap.Id,
                MapRemovePOICommand.write(SectorControlVisualHash));
        }

        private string GetSectorControlZoneSpecification()
        {
            var displayedFactionId = capturingFactionId > 0 ? capturingFactionId : FactionId;

            switch (displayedFactionId)
            {
                case FactionModule.MMO:
                    return "MMO";
                case FactionModule.EIC:
                    return "EIC";
                case FactionModule.VRU:
                    return "VRU";
                default:
                    return "NONE";
            }
        }

        private void HandleClanClick(Player player)
        {
            if (player.Clan == null || player.Clan.Id == 0)
            {
                player.SendCommand(BattleStationNoClanUiInitializationCommand.write(Id));
                player.SendPacket("0|A|STD|You need to be in a clan to use clan battle stations.");
                return;
            }

            SendClanInterfaceCommand(player);

            if (!IsOperational)
            {
                if (!CanClanUseMap(player.Clan, player.Spacemap))
                    player.SendPacket("0|A|STD|This clan battle station cannot be built on an enemy faction map.");
                else if (!HasRequiredCoreModules())
                    player.SendPacket("0|A|STD|Instale primeiro os modulos centrais Hull e Deflector para iniciar a construcao.");

                return;
            }

            player.SendPacket($"0|A|STD|Battle station owner: {GetOwnerName()}. Level {GetEffectiveLevel()}.");
        }

        private void ProcessCapture()
        {
            var now = DateTime.Now;

            if (lastCaptureProgressAt == DateTime.MinValue)
            {
                lastCaptureProgressAt = now;
                return;
            }

            var elapsedSeconds = (int)(now - lastCaptureProgressAt).TotalSeconds;
            if (elapsedSeconds <= 0)
                return;

            lastCaptureProgressAt = lastCaptureProgressAt.AddSeconds(elapsedSeconds);

            var captureState = GetCaptureState();
            var minimumPlayers = Math.Max(1, Definition.MinPlayersToCapture);
            if (captureState.Contested || captureState.ActiveFactionId == 0 || captureState.PlayerCount < minimumPlayers)
                return;

            var pointsPerSecond = Math.Min(captureState.PlayerCount, GetMaxCapturePointsPerSecond());

            for (var second = 0; second < elapsedSeconds; second++)
            {
                ApplyCaptureProgress(captureState.ActiveFactionId, pointsPerSecond);

                if (FactionId != 0)
                    break;
            }
        }

        private void Claim(int factionId)
        {
            var previousAssetType = AssetTypeId;

            FactionId = factionId;
            Clan = GameManager.GetClan(0);
            AssetTypeId = Definition.StationAssetTypeId;
            SetUpgradeLevel(1, true);

            SpawnDefenseTowers();
            ResetCaptureProgress();
            previousVulnerabilityState = IsCurrentlyVulnerable();
            UpdateShieldState(true);

            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(new AssetTypeModule(previousAssetType), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
            RemoveSectorControlPOI();
            GameManager.SendPacketToAll($"0|A|STD|Battle station {AsteroidName} on {Spacemap.Name} was captured by {GetFactionName(FactionId)} at level 1.");

            QueryManager.BattleStations.BattleStation(this);
            RefreshBoosterInterface();
        }

        private void Neutralize()
        {
            var previousAssetType = AssetTypeId;

            ClearStationVisualEffects();
            RemoveStationSatellites();
            ResetCaptureProgress();

            FactionId = 0;
            Level = 0;
            Clan = GameManager.GetClan(0);
            AssetTypeId = IsFactionBattleStation ? Definition.AsteroidAssetTypeId : AssetTypeModule.ASTEROID;
            ApplyLevelStats(true);
            Invincible = true;
            InBuildingState = false;
            DeflectorActive = false;
            DeflectorSecondsLeft = 0;
            DeflectorSecondsMax = 0;
            previousVulnerabilityState = Definition != null && Definition.IsVulnerableAt(DateTime.Now);
            RemoveVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR);

            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(new AssetTypeModule(previousAssetType), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
            EnsureSectorControlBeaconPOI();
            UpdateSectorControlPOI();
            RefreshBoosterInterface();
        }

        public void RefreshBoosterInterface()
        {
            if (Spacemap == null)
                return;

            foreach (var player in Spacemap.Characters.Values.OfType<Player>())
                player.BoosterManager?.Update();
        }

        private void SpawnDefenseTowers()
        {
            RemoveStationSatellites();

            foreach (var towerDefinition in Definition.Towers)
            {
                var tower = new Satellite(this, towerDefinition, Satellite.GetPosition(Position, towerDefinition.SlotId));
                DefenseTowers.Add(tower);
                Spacemap.Activatables.TryAdd(tower.Id, tower);
                GameManager.SendCommandToMap(Spacemap.Id, tower.GetAssetCreateCommand());
            }
        }

        private void RemoveDefenseTowers()
        {
            foreach (var tower in DefenseTowers.ToList())
            {
                tower.Remove();
                Spacemap.Activatables.TryRemove(tower.Id, out var removedTower);
                GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(tower.GetAssetType(), tower.Id));
            }

            DefenseTowers.Clear();
        }

        private void RemoveStationSatellites()
        {
            RemoveDefenseTowers();

            var equippedSatellites = EquippedStationModule.Values
                .SelectMany(x => x ?? new List<Satellite>())
                .Where(x => x != null)
                .ToList();

            foreach (var satellite in equippedSatellites)
            {
                satellite.Remove(false, true, true);

                if (Spacemap != null && ShouldDisplayModuleAsSatellite(satellite.SlotId))
                {
                    Spacemap.Activatables.TryRemove(satellite.Id, out var removedSatellite);
                    GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(satellite.GetAssetType(), satellite.Id));
                }
            }

            EquippedStationModule.Clear();
        }

        private void ProcessClanBuild()
        {
            if (!InBuildingState)
                return;

            if (buildTime == DateTime.MinValue || buildTime > DateTime.Now)
                return;

            ActivateClanStation();
        }

        private void ActivateClanStation()
        {
            var previousAssetType = AssetTypeId;

            InBuildingState = false;
            AssetTypeId = AssetTypeModule.BATTLESTATION;
            Level = Math.Max(1, Level);
            ApplyLevelStats(true);
            Invincible = false;
            SyncClanConstructionVisual();

            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(new AssetTypeModule(previousAssetType), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());
            SyncVisibleClanModules();
            QueryManager.BattleStations.BattleStation(this);
        }

        private void ResetCaptureProgress()
        {
            capturingFactionId = 0;
            captureProgressPoints = 0;
            lastCaptureProgressAt = DateTime.MinValue;
        }

        private void UpdateSectorControlProgressBars()
        {
            if (!IsSectorControlStation || Definition == null || Spacemap == null)
                return;

            var playersById = Spacemap.Characters.Values
                .OfType<Player>()
                .Where(player => player != null)
                .ToDictionary(player => player.Id);

            var playersInCaptureZone = FactionId == 0
                ? playersById.Values
                    .Where(player => !player.Destroyed && player.CurrentHitPoints > 0 && player.FactionId > 0 && player.Position.DistanceTo(Position) <= Definition.CaptureRadius)
                    .ToList()
                : new List<Player>();

            var playersInCaptureZoneIds = new HashSet<int>(playersInCaptureZone.Select(player => player.Id));

            List<int> playersLeavingCaptureZone;
            lock (sectorControlProgressBarPlayersLock)
            {
                playersLeavingCaptureZone = sectorControlProgressBarPlayers
                    .Where(id => !playersInCaptureZoneIds.Contains(id))
                    .ToList();

                foreach (var playerId in playersLeavingCaptureZone)
                {
                    sectorControlProgressBarPlayers.Remove(playerId);
                    sectorControlProgressBarVisibilityPending.Remove(playerId);
                    if (playersById.TryGetValue(playerId, out var player))
                        SendSectorControlCommand(
                            player,
                            "SectorControlBeaconProgressVisibilityCommand",
                            SectorControlBeaconProgressVisibilityCommand.ID,
                            SectorControlBeaconProgressVisibilityCommand.write(SectorControlHash, false),
                            "LEAVE_RANGE");
                }
            }

            var factions = GetSectorControlCapturingFactions();
            var progressPercent = GetSectorControlProgressPercent();
            var factionsSnapshot = string.Join(",", factions);
            var shouldBroadcast = progressPercent != lastSectorControlProgressPercent
                || capturingFactionId != lastSectorControlCapturingFactionId
                || factionsSnapshot != lastSectorControlCapturingFactions;

            foreach (var player in playersInCaptureZone)
            {
                bool enteredCaptureZone;
                lock (sectorControlProgressBarPlayersLock)
                {
                    if (!Spacemap.Characters.TryGetValue(player.Id, out var currentCharacter)
                        || !ReferenceEquals(currentCharacter, player))
                        continue;

                    enteredCaptureZone = sectorControlProgressBarPlayers.Add(player.Id);
                    if (enteredCaptureZone)
                    {
                        if (!Spacemap.POIs.TryGetValue(SectorControlHash, out var sectorPoi) || sectorPoi == null)
                        {
                            sectorControlProgressBarPlayers.Remove(player.Id);
                            continue;
                        }

                        // A station created after map initialization may not
                        // have been included in SendObjects.  Register its
                        // beacon once for this player before sending 15267.
                        if (!sectorControlPoiPlayers.Contains(player.Id))
                        {
                            SendSectorControlPOI(
                                player,
                                sectorPoi.GetPOICreateCommand(),
                                "ENTER_RANGE_REGISTER_BEACON");
                        }
                    }

                    if (enteredCaptureZone || shouldBroadcast)
                        SendSectorControlCommand(
                            player,
                            "SectorControlBeaconUpdateCommand",
                            SectorControlBeaconUpdateCommand.ID,
                            SectorControlBeaconUpdateCommand.write(
                                progressPercent,
                                0,
                                factions,
                                capturingFactionId,
                                SectorControlHash),
                            enteredCaptureZone ? "ENTER_RANGE_SNAPSHOT" : "PROGRESS_UPDATE");

                    if (enteredCaptureZone)
                    {
                        // Let the client finish registering/updating the
                        // beacon before its visibility handler touches the
                        // progress-bar model.  The command is sent on the next
                        // station Tick (normally ~84 ms later).
                        sectorControlProgressBarVisibilityPending.Add(player.Id);
                    }
                    else if (sectorControlProgressBarVisibilityPending.Remove(player.Id))
                    {
                        SendSectorControlCommand(
                            player,
                            "SectorControlBeaconProgressVisibilityCommand",
                            SectorControlBeaconProgressVisibilityCommand.ID,
                            SectorControlBeaconProgressVisibilityCommand.write(SectorControlHash, true),
                            "ENTER_RANGE_SHOW_DELAYED");
                    }
                }
            }

            if (shouldBroadcast)
            {
                lastSectorControlProgressPercent = progressPercent;
                lastSectorControlCapturingFactionId = capturingFactionId;
                lastSectorControlCapturingFactions = factionsSnapshot;
            }
        }

        public void HideSectorControlProgressBar(Player player)
        {
            if (player == null)
                return;

            lock (sectorControlProgressBarPlayersLock)
            {
                sectorControlProgressBarPlayers.Remove(player.Id);
                sectorControlPoiPlayers.Remove(player.Id);
                sectorControlProgressBarVisibilityPending.Remove(player.Id);
                SendSectorControlCommand(
                    player,
                    "SectorControlBeaconProgressVisibilityCommand",
                    SectorControlBeaconProgressVisibilityCommand.ID,
                    SectorControlBeaconProgressVisibilityCommand.write(SectorControlHash, false),
                    "REMOVE_CHARACTER");
            }
        }

        private List<int> GetSectorControlCapturingFactions()
        {
            var factions = Spacemap.Characters.Values
                .OfType<Player>()
                .Where(player => !player.Destroyed && player.CurrentHitPoints > 0 && player.FactionId > 0 && player.Position.DistanceTo(Position) <= Definition.CaptureRadius)
                .Select(player => player.FactionId)
                .Distinct()
                .OrderBy(factionId => factionId == capturingFactionId ? 0 : 1)
                .ThenBy(factionId => factionId)
                .ToList();

            return factions;
        }

        private int GetSectorControlProgressPercent()
        {
            var required = GetCapturePointsRequired();
            if (required <= 0)
                return 0;

            return Math.Max(0, Math.Min(100, captureProgressPoints * 100 / required));
        }

        private void SendSectorControlCommand(Player player, string commandName, short commandId, byte[] command, string reason)
        {
            if (player == null || command == null)
                return;

            PacketDebug.Log("sector_control_packets",
                $"QUEUE {commandName} ID={commandId} LENGTH={command.Length} PLAYER={player.Id} MAP={player.Spacemap?.Id ?? -1} HASH={SectorControlHash} REASON={reason}");
            player.SendCommand(command);
            if (PacketDebug.Enabled)
            {
                player.SendPacket(
                    $"0|A|STD|[PACKET_DEBUG] AFTER {commandName} ID={commandId} LENGTH={command.Length} MAP={player.Spacemap?.Id ?? -1} HASH={SectorControlHash} REASON={reason}");
            }
        }

        private void ApplyCaptureProgress(int factionId, int points)
        {
            if (factionId <= 0 || points <= 0 || FactionId != 0)
                return;

            if (capturingFactionId == 0 || captureProgressPoints <= 0)
            {
                capturingFactionId = factionId;
                captureProgressPoints = Math.Min(GetCapturePointsRequired(), points);
            }
            else if (capturingFactionId == factionId)
            {
                captureProgressPoints = Math.Min(GetCapturePointsRequired(), captureProgressPoints + points);
            }
            else
            {
                captureProgressPoints = Math.Max(0, captureProgressPoints - points);

                if (captureProgressPoints == 0)
                    capturingFactionId = 0;

                return;
            }

            if (captureProgressPoints >= GetCapturePointsRequired())
                Claim(factionId);
        }

        private int GetCapturePointsRequired()
        {
            return Definition?.CapturePointsRequired > 0 ? Definition.CapturePointsRequired : 100;
        }

        private int GetMaxCapturePointsPerSecond()
        {
            return Definition?.MaxCapturePointsPerSecond > 0 ? Definition.MaxCapturePointsPerSecond : 10;
        }

        private CaptureState GetCaptureState()
        {
            var contenders = Spacemap.Characters.Values
                .OfType<Player>()
                .Where(player => !player.Destroyed && player.CurrentHitPoints > 0 && player.FactionId > 0 && player.Position.DistanceTo(Position) <= Definition.CaptureRadius)
                .GroupBy(player => player.FactionId)
                .Select(group => new { FactionId = group.Key, PlayerCount = group.Count() })
                .ToList();

            if (contenders.Count != 1)
                return new CaptureState(0, 0, contenders.Count > 1);

            return new CaptureState(contenders[0].FactionId, contenders[0].PlayerCount, false);
        }

        private void ApplyLevelStats(bool restoreCurrent)
        {
            if (Definition == null)
            {
                MaxHitPoints = MaxHitPoints > 0 ? MaxHitPoints : DefaultClanStationHitPoints;
                MaxShieldPoints = MaxShieldPoints > 0 ? MaxShieldPoints : DefaultClanStationShieldPoints;
            }
            else
            {
                var stats = Definition.GetCenterLevelDefinition(GetEffectiveLevel());
                MaxHitPoints = stats.MaxHitPoints > 0 ? stats.MaxHitPoints : Definition.MaxHitPoints;
                MaxShieldPoints = stats.MaxShieldPoints > 0 ? stats.MaxShieldPoints : Definition.MaxShieldPoints;
            }

            if (restoreCurrent || CurrentHitPoints > MaxHitPoints)
                CurrentHitPoints = MaxHitPoints;
            if (restoreCurrent || CurrentShieldPoints > MaxShieldPoints)
                CurrentShieldPoints = MaxShieldPoints;

            UpdateStatus();
        }

        private void HandleVulnerabilitySurvived()
        {
            var nextLevel = Definition.GetNextLevel(UpgradeLevel);
            if (nextLevel > UpgradeLevel)
            {
                SetUpgradeLevel(nextLevel, true);
                TriggerLevelUpVisualEffect();
                GameManager.SendPacketToAll($"0|A|STD|Battle station {AsteroidName} on {Spacemap.Name} survived the vulnerability window and reached level {GetEffectiveLevel()}.");
            }
            else
            {
                GameManager.SendPacketToAll($"0|A|STD|Battle station {AsteroidName} on {Spacemap.Name} survived the vulnerability window and remains at max level.");
            }

            QueryManager.BattleStations.BattleStation(this);
        }

        private void UpdateShieldState(bool forceRefresh = false)
        {
            if (Definition == null)
                return;

            var shouldEnableShield = FactionId != 0 && !IsCurrentlyVulnerable();
            var shieldWasActive = DeflectorActive;

            if (!forceRefresh && shouldEnableShield == DeflectorActive)
            {
                DeflectorSecondsLeft = Definition.GetSecondsUntilStateChange(DateTime.Now);
                DeflectorSecondsMax = DeflectorSecondsLeft;
                return;
            }

            DeflectorActive = shouldEnableShield;
            Invincible = shouldEnableShield;
            DeflectorSecondsLeft = Definition.GetSecondsUntilStateChange(DateTime.Now);
            DeflectorSecondsMax = DeflectorSecondsLeft;
            deflectorTime = DateTime.Now;

            if (shouldEnableShield)
            {
                SetConstructionVisualState(false);
                AddVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR, DeflectorSecondsLeft, "", 0, true);
            }
            else
            {
                RemoveVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR);
                SetConstructionVisualState(true);
            }

            if (shouldEnableShield && !shieldWasActive)
                RestoreDestroyedTowers();
        }

        private void SetConstructionVisualState(bool constructionActive)
        {
            if (constructionActive)
                AddVisualModifier(VisualModifierCommand.CONSTRUCTION_EFFECT, 0, "", 0, true);
            else
                RemoveVisualModifier(VisualModifierCommand.CONSTRUCTION_EFFECT);

            foreach (var tower in DefenseTowers.Where(x => x != null))
            {
                if (constructionActive && !tower.Destroyed && !tower.IsDestroyedModuleState)
                    tower.AddVisualModifier(VisualModifierCommand.MODULE_INSTALL_EFFECT, 0, "", 0, true);
                else
                    tower.RemoveVisualModifier(VisualModifierCommand.MODULE_INSTALL_EFFECT);
            }
        }

        private void TriggerLevelUpVisualEffect()
        {
            _ = PlayTemporaryVisualModifier(VisualModifierCommand.MODULE_LEVEL_UP_EFFECT, 2000);

            foreach (var tower in GetSatellites().Where(x => x != null && !x.Destroyed))
                _ = tower.PlayTemporaryVisualModifier(VisualModifierCommand.MODULE_LEVEL_UP_EFFECT, 2000);
        }

        private void SyncClanConstructionVisual()
        {
            if (!IsClanBattleStation)
                return;

            if (InBuildingState)
                AddVisualModifier(VisualModifierCommand.BATTLESTATION_CONSTRUCTING, 0, "", 0, true);
            else
                RemoveVisualModifier(VisualModifierCommand.BATTLESTATION_CONSTRUCTING);
        }

        private void ClearStationVisualEffects()
        {
            RemoveVisualModifier(VisualModifierCommand.BATTLESTATION_DEFLECTOR);
            RemoveVisualModifier(VisualModifierCommand.CONSTRUCTION_EFFECT);
            RemoveVisualModifier(VisualModifierCommand.MODULE_LEVEL_UP_EFFECT);

            foreach (var tower in GetSatellites().Where(x => x != null))
            {
                tower.RemoveVisualModifier(VisualModifierCommand.MODULE_INSTALL_EFFECT);
                tower.RemoveVisualModifier(VisualModifierCommand.MODULE_LEVEL_UP_EFFECT);
            }
        }

        public static string GetFactionName(int factionId)
        {
            switch (factionId)
            {
                case 1:
                    return "MMO";
                case 2:
                    return "EIC";
                case 3:
                    return "VRU";
                default:
                    return "Neutral";
            }
        }

        public int GetBoostPercentage(BoostedAttributeType boostedAttributeType)
        {
            if (!IsOwned)
                return 0;

            return GetSatellites()
                .Where(x => x != null && !x.Destroyed)
                .Sum(x => x.GetBoostPercentage(boostedAttributeType));
        }

        public static int GetFactionBoostPercentage(int factionId, BoostedAttributeType boostedAttributeType)
        {
            if (factionId <= 0)
                return 0;

            return GameManager.BattleStations.Values
                .Where(x => x != null && x.IsFactionBattleStation && x.FactionId == factionId && !x.Destroyed)
                .Sum(x => x.GetBoostPercentage(boostedAttributeType));
        }

        public static int GetPlayerBoostPercentage(Player player, BoostedAttributeType boostedAttributeType)
        {
            if (player == null)
                return 0;

            var factionBoost = GetFactionBoostPercentage(player.FactionId, boostedAttributeType);
            if (player.Clan == null || player.Clan.Id == 0)
                return factionBoost;

            var clanBoost = GameManager.BattleStations.Values
                .Where(x => x != null
                    && x.IsClanBattleStation
                    && !x.Destroyed
                    && x.Clan != null
                    && x.Clan.Id == player.Clan.Id)
                .Sum(x =>
                {
                    var boost = x.GetBoostPercentage(boostedAttributeType);
                    if (boost <= 0)
                        return 0;

                    if (player.Spacemap != null && x.Spacemap != null && player.Spacemap.Id == x.Spacemap.Id)
                        return boost;

                    return boost * ClanOffMapBoostPercentage / 100;
                });

            return factionBoost + clanBoost;
        }

        private int GetClanModuleUpgradeLevel(int itemId)
        {
            if (Clan == null || Clan.Id == 0)
                return 0;

            return Clan.BattleStationInventory.FirstOrDefault(x => x.ItemId == itemId)?.UpgradeLevel ?? 0;
        }

        private string GetClanModuleDisplayLabel(int upgradeLevel)
        {
            var clanName = Clan?.Name ?? "Clan";
            return $"{clanName} U{Math.Max(0, upgradeLevel)}";
        }

        private bool HasInstalledCoreModule(short type, int slotId)
        {
            var module = GetSatelliteBySlotId(slotId);
            return module != null
                && module.Installed
                && !module.Destroyed
                && !module.IsDestroyedModuleState
                && module.Type == type;
        }

        public bool HasRequiredCoreModules()
        {
            return HasInstalledCoreModule(StationModuleModule.HULL, 0)
                && HasInstalledCoreModule(StationModuleModule.DEFLECTOR, 1);
        }

        private int GetInstalledCoreUpgradeLevel(short type, int slotId)
        {
            var module = GetSatelliteBySlotId(slotId);

            if (module == null
                || !module.Installed
                || module.Destroyed
                || module.IsDestroyedModuleState
                || module.Type != type)
                return 0;

            return module.UpgradeLevel;
        }

        private int ResolveClanLevelFromModules()
        {
            if (!IsClanBattleStation)
                return Level > 0 ? Level : 1;

            var highestUpgrade = Math.Max(
                GetInstalledCoreUpgradeLevel(StationModuleModule.HULL, 0),
                GetInstalledCoreUpgradeLevel(StationModuleModule.DEFLECTOR, 1));

            if (highestUpgrade >= 16)
                return 3;

            if (highestUpgrade > 8)
                return 2;

            return 1;
        }

        private void RecalculateClanLevel(bool restoreCurrent)
        {
            if (!IsClanBattleStation)
                return;

            var previousLevel = Level;
            Level = ResolveClanLevelFromModules();
            var levelChanged = previousLevel != Level;

            ApplyLevelStats(restoreCurrent);

            foreach (var tower in GetSatellites().Where(x => x != null && !x.Destroyed))
                tower.ApplyLevelStats(restoreCurrent);

            if (levelChanged)
            {
                RefreshVisual();
                RefreshBoosterInterface();
            }
        }

        public int GetEffectiveLevel()
        {
            if (Definition == null)
                return ResolveClanLevelFromModules();

            return UpgradeLevel > 0 ? UpgradeLevel : Definition.GetMinLevel();
        }

        public bool SetUpgradeLevel(int level, bool restoreCurrent = true)
        {
            if (!IsOwned)
                return false;

            var previousLevel = Level;
            Level = Definition != null ? Definition.NormalizeUpgradeLevel(level) : Math.Max(1, level);
            var levelChanged = previousLevel != Level;
            ApplyLevelStats(restoreCurrent);

            foreach (var tower in GetSatellites().Where(x => x != null && !x.Destroyed))
            {
                tower.ApplyLevelStats(restoreCurrent);

                if (levelChanged)
                    tower.RefreshVisual(true);
            }

            if (levelChanged)
                RefreshVisual();

            QueryManager.BattleStations.BattleStation(this);

            if (levelChanged)
                RefreshBoosterInterface();

            return true;
        }

        public override int GetVisualDesignId()
        {
            var factionVisualIndex = GetAffiliatedFactionId() > 0 ? GetAffiliatedFactionId() - 1 : 0;
            var hullVisualIndex = Definition != null ? Definition.GetCenterVisualIndex(GetEffectiveLevel()) : 0;
            return (factionVisualIndex << 16) | (hullVisualIndex & 0xFFFF);
        }

        private static int GetClientExpansionStage(int level)
        {
            switch (level)
            {
                case 1:
                    return 1;
                case 2:
                    return 10;
                case 3:
                    return 16;
                default:
                    return Math.Max(1, level);
            }
        }

        public override int GetVisualExpansionStage()
        {
            if (Definition == null)
            {
                var clanVisualStage = GetClientExpansionStage(GetEffectiveLevel());
                return (clanVisualStage << 16) | (clanVisualStage & 0xFFFF);
            }

            var effectiveLevel = GetEffectiveLevel();
            var stats = Definition.GetCenterLevelDefinition(effectiveLevel);
            var visualStage = stats.ExpansionStage > 0 ? stats.ExpansionStage : GetClientExpansionStage(effectiveLevel);

            return (visualStage << 16) | (visualStage & 0xFFFF);
        }

        private void RefreshVisual()
        {
            GameManager.SendCommandToMap(Spacemap.Id, AssetRemoveCommand.write(GetAssetType(), Id));
            GameManager.SendCommandToMap(Spacemap.Id, GetAssetCreateCommand());

            if (IsOwned)
                BroadcastStatusCommand();
        }

        private void RestoreDestroyedTowers()
        {
            foreach (var tower in GetSatellites().Where(x => x != null && x.IsDestroyedModuleState).ToList())
                tower.RestoreFromDestroyedState();
        }

        private class CaptureState
        {
            public int ActiveFactionId { get; private set; }
            public int PlayerCount { get; private set; }
            public bool Contested { get; private set; }

            public CaptureState(int activeFactionId, int playerCount, bool contested)
            {
                ActiveFactionId = activeFactionId;
                PlayerCount = playerCount;
                Contested = contested;
            }
        }

        public BattleStationStatusCommand GetStatusCommand()
        {
            return new BattleStationStatusCommand(
                Id,
                Id,
                Name,
                DeflectorActive,
                DeflectorSecondsLeft,
                DeflectorSecondsMax,
                GetAttackRating(),
                GetDefenceRating(),
                GetRepairRating(),
                GetHonorBoosterRating(),
                GetExperienceBoosterRating(),
                GetDamageBoosterRating(),
                GetDeflectorShieldRate(),
                Definition?.RepairPrice ?? 0,
                new EquippedModulesModule(GetStatusModules()));
        }

        private IEnumerable<Satellite> GetActiveTowers()
        {
            return GetSatellites().Where(x => x != null && x.Installed && !x.Destroyed && !x.IsDestroyedModuleState);
        }

        private BattleStationLevelDefinition GetTowerStats(Satellite tower)
        {
            if (tower == null || tower.TowerDefinition == null)
                return null;

            var towerLevel = tower.UpgradeLevel > 0 ? tower.UpgradeLevel : GetEffectiveLevel();
            return tower.TowerDefinition.GetLevelDefinition(towerLevel);
        }

        private int GetAttackRating()
        {
            return GetActiveTowers()
                .Where(x => x.Type == StationModuleModule.LASER_HIGH_RANGE
                    || x.Type == StationModuleModule.LASER_MID_RANGE
                    || x.Type == StationModuleModule.LASER_LOW_RANGE
                    || x.Type == StationModuleModule.ROCKET_MID_ACCURACY
                    || x.Type == StationModuleModule.ROCKET_LOW_ACCURACY)
                .Sum(x => GetTowerStats(x)?.Damage ?? GetDefaultAttackRating(x.Type));
        }

        private int GetDefenceRating()
        {
            var stationDefence = Math.Max(0, MaxHitPoints) + Math.Max(0, MaxShieldPoints);
            var towerDefence = GetActiveTowers().Sum(x => Math.Max(0, x.MaxHitPoints) + Math.Max(0, x.MaxShieldPoints));
            return stationDefence + towerDefence;
        }

        private int GetRepairRating()
        {
            return GetActiveTowers()
                .Where(x => x.Type == StationModuleModule.REPAIR)
                .Sum(x => GetTowerStats(x)?.RepairAmount ?? 5000);
        }

        private int GetHonorBoosterRating()
        {
            return GetBoostPercentage(BoostedAttributeType.HONOUR);
        }

        private int GetExperienceBoosterRating()
        {
            return GetBoostPercentage(BoostedAttributeType.EP);
        }

        private int GetDamageBoosterRating()
        {
            return GetBoostPercentage(BoostedAttributeType.DAMAGE);
        }

        private int GetDeflectorShieldRate()
        {
            if (DeflectorSecondsMax <= 0)
                return DeflectorActive ? 100 : 0;

            var rate = (int)Math.Round((double)Math.Max(0, DeflectorSecondsLeft) * 100 / DeflectorSecondsMax);
            return Math.Max(0, Math.Min(100, rate));
        }

        public void SendStatusCommand(Player player)
        {
            if (player == null || !IsOwned)
                return;

            player.SendCommand(GetStatusCommand().writeCommand());
        }

        private void BroadcastStatusCommand()
        {
            if (!IsOwned)
                return;

            GameManager.SendCommandToMap(Spacemap.Id, GetStatusCommand().writeCommand());
        }

        private List<StationModuleModule> GetStatusModules()
        {
            var hullModule = GetSatelliteBySlotId(0);
            var deflectorModule = GetSatelliteBySlotId(1);

            var modules = new List<StationModuleModule>
            {
                hullModule != null
                    ? CreateStatusModule(
                        hullModule.SlotId,
                        hullModule.Type,
                        hullModule.CurrentHitPoints,
                        hullModule.MaxHitPoints,
                        hullModule.CurrentShieldPoints,
                        hullModule.MaxShieldPoints,
                        hullModule.UpgradeLevel > 0 ? hullModule.UpgradeLevel : GetClanModuleUpgradeLevel(hullModule.ItemId),
                        hullModule.EmergencyRepairActive ? 1 : 0,
                        hullModule.EmergencyRepairActive ? 1 : 0,
                        hullModule.ItemId,
                        0,
                        GetClanModuleDisplayLabel(hullModule.UpgradeLevel > 0 ? hullModule.UpgradeLevel : GetClanModuleUpgradeLevel(hullModule.ItemId)),
                        hullModule.Installed ? 0 : hullModule.InstallationSecondsLeft,
                        hullModule.InstallationSecondsLeft)
                    : CreateStatusModule(0, StationModuleModule.NONE, 0, 0, 0, 0, 0, 0, 0),
                deflectorModule != null
                    ? CreateStatusModule(
                        deflectorModule.SlotId,
                        deflectorModule.Type,
                        deflectorModule.CurrentHitPoints,
                        deflectorModule.MaxHitPoints,
                        deflectorModule.CurrentShieldPoints,
                        deflectorModule.MaxShieldPoints,
                        deflectorModule.UpgradeLevel > 0 ? deflectorModule.UpgradeLevel : GetClanModuleUpgradeLevel(deflectorModule.ItemId),
                        deflectorModule.EmergencyRepairActive ? 1 : 0,
                        deflectorModule.EmergencyRepairActive ? 1 : 0,
                        deflectorModule.ItemId,
                        0,
                        GetClanModuleDisplayLabel(deflectorModule.UpgradeLevel > 0 ? deflectorModule.UpgradeLevel : GetClanModuleUpgradeLevel(deflectorModule.ItemId)),
                        deflectorModule.Installed ? 0 : deflectorModule.InstallationSecondsLeft,
                        deflectorModule.InstallationSecondsLeft)
                    : CreateStatusModule(1, StationModuleModule.NONE, 0, 0, 0, 0, 0, 0, 0)
            };

            foreach (var tower in GetSatellites().Where(x => x != null && x.SlotId >= 2))
            {
                modules.Add(CreateStatusModule(
                    tower.SlotId,
                    tower.Type,
                    tower.CurrentHitPoints,
                    tower.MaxHitPoints,
                    tower.CurrentShieldPoints,
                    tower.MaxShieldPoints,
                    tower.UpgradeLevel > 0 ? tower.UpgradeLevel : GetClanModuleUpgradeLevel(tower.ItemId),
                    tower.EmergencyRepairActive ? 1 : 0,
                    tower.EmergencyRepairActive ? 1 : 0,
                    tower.ItemId,
                    0,
                        IsClanBattleStation ? GetClanModuleDisplayLabel(tower.UpgradeLevel > 0 ? tower.UpgradeLevel : GetClanModuleUpgradeLevel(tower.ItemId)) : tower.Name,
                    tower.Installed ? 0 : tower.InstallationSecondsLeft,
                    tower.InstallationSecondsLeft));
            }

            return modules;
        }

        private StationModuleModule CreateStatusModule(int slotId, short type, int currentHitpoints, int maxHitpoints, int currentShield, int maxShield, int upgradeLevel, int emergencyRepairSecondsLeft, int emergencyRepairSecondsTotal, int itemId = 0, int ownerId = 0, string ownerName = "", int installationSeconds = 0, int installationSecondsLeft = 0)
        {
            return new StationModuleModule(
                Id,
                itemId,
                slotId,
                type,
                currentHitpoints,
                maxHitpoints,
                currentShield,
                maxShield,
                upgradeLevel,
                string.IsNullOrWhiteSpace(ownerName) ? ownerId.ToString() : ownerName,
                installationSeconds,
                installationSecondsLeft,
                emergencyRepairSecondsLeft,
                emergencyRepairSecondsTotal,
                0);
        }

        private int GetAffiliatedFactionId()
        {
            return IsClanBattleStation ? ResolveClanFactionId(Clan, FactionId) : FactionId;
        }

        private string GetOwnerName()
        {
            if (IsClanBattleStation)
                return Clan != null && Clan.Id != 0 ? Clan.Name : "Neutral";

            return GetFactionName(FactionId);
        }

        private int GetSecondsUntilBuildComplete()
        {
            if (!InBuildingState || buildTime == DateTime.MinValue)
                return 0;

            return Math.Max(0, (int)Math.Ceiling((buildTime - DateTime.Now).TotalSeconds));
        }

        private static int ResolveClanFactionId(Clan clan, int fallbackFactionId)
        {
            if (clan == null || clan.Id == 0)
                return fallbackFactionId;

            return clan.FactionId > 0 ? clan.FactionId : fallbackFactionId;
        }

        private static int GetDefaultAttackRating(short type)
        {
            switch (type)
            {
                case StationModuleModule.LASER_LOW_RANGE:
                    return 1000;
                case StationModuleModule.LASER_MID_RANGE:
                    return 1400;
                case StationModuleModule.LASER_HIGH_RANGE:
                    return 1800;
                case StationModuleModule.ROCKET_MID_ACCURACY:
                    return 1400;
                case StationModuleModule.ROCKET_LOW_ACCURACY:
                    return 1800;
                default:
                    return 0;
            }
        }
    }
}
