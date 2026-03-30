using Newtonsoft.Json;
using Ow.Game.Movements;
using Ow.Game.Objects;
using Ow.Game.Objects.Players;
using Ow.Game.Objects.Players.Managers;
using Ow.Game.Ticks;
using Ow.Managers;
using Ow.Managers.MySQLManager;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ow.Game.GalaxyGates
{
    class GalaxyGateManager
    {
        private readonly object gateLock = new object();
        private readonly Dictionary<int, GalaxyGateTemplate> templates = new Dictionary<int, GalaxyGateTemplate>();
        private readonly Dictionary<int, GalaxyGateInstance> instancesById = new Dictionary<int, GalaxyGateInstance>();
        private readonly Dictionary<int, int> ownerToInstance = new Dictionary<int, int>();
        private int nextDynamicMapId = 10000;
        private bool initialized;

        public void Initialize()
        {
            lock (gateLock)
            {
                if (initialized)
                    return;

                GalaxyGateRepository.EnsureSchema();
                foreach (var template in GalaxyGateRepository.LoadTemplates())
                    templates[template.Id] = template;

                foreach (var template in templates.Values)
                    SpawnTemplatePortal(template);

                foreach (var instance in GalaxyGateRepository.LoadActiveInstances(templates))
                {
                    instancesById[instance.Id] = instance;
                    if (!ownerToInstance.ContainsKey(instance.OwnerId))
                        ownerToInstance[instance.OwnerId] = instance.Id;
                    EnsureEntryPortal(instance);
                }

                initialized = true;
            }

            Task.Factory.StartNew(RunLoop, TaskCreationOptions.LongRunning);
        }

        private void RunLoop()
        {
            while (true)
            {
                try
                {
                    List<GalaxyGateInstance> snapshot;
                    lock (gateLock)
                        snapshot = instancesById.Values.ToList();

                    foreach (var instance in snapshot)
                        EnsureState(instance);
                }
                catch (Exception e)
                {
                    Logger.Log("error_log", $"- [GalaxyGateManager.cs] RunLoop exception: {e}");
                }

                Thread.Sleep(2000);
            }
        }

        private void EnsureState(GalaxyGateInstance instance)
        {
            if (instance == null || instance.Template == null)
                return;

            if (instance.Completed || instance.Failed)
            {
                RemoveEntryPortal(instance);
                return;
            }

            EnsureEntryPortal(instance);

            if (instance.Spacemap == null)
                return;

            if (IsCurrentWaveCompleted(instance))
                EnsureDecisionPortals(instance);
        }

        private void SpawnTemplatePortal(GalaxyGateTemplate template)
        {
            var targetPosition = template.GateCenterPosition ?? new Position(11100, 6500);
            var defaultMap = template.EntryMapId;
            var defaultPos = template.EntryPortalPosition ?? new Position(11100, 6500);

            SpawnTemplatePortalForFaction(template, 1, template.EntryMapIdMmo > 0 ? template.EntryMapIdMmo : defaultMap, template.EntryPortalPositionMmo ?? defaultPos, targetPosition);
            SpawnTemplatePortalForFaction(template, 2, template.EntryMapIdEic > 0 ? template.EntryMapIdEic : defaultMap, template.EntryPortalPositionEic ?? defaultPos, targetPosition);
            SpawnTemplatePortalForFaction(template, 3, template.EntryMapIdVru > 0 ? template.EntryMapIdVru : defaultMap, template.EntryPortalPositionVru ?? defaultPos, targetPosition);
        }

        private void SpawnTemplatePortalForFaction(GalaxyGateTemplate template, int factionId, int mapId, Position position, Position targetPosition)
        {
            var map = GameManager.GetSpacemap(mapId);
            if (map == null)
                return;

            var portal = new GalaxyGateEntryPortal(this, template, map, position, targetPosition, template.EntryPortalGraphicId, factionId);
            GameManager.SendCommandToMap(map.Id, portal.GetAssetCreateCommand());
        }

        private void EnsureEntryPortal(GalaxyGateInstance instance)
        {
            if (instance == null || instance.Template == null)
                return;

            int mapId;
            Position position;
            GetEntryMapForFaction(instance.Template, instance.OwnerFactionId, out mapId, out position);

            var map = GameManager.GetSpacemap(mapId);
            if (map == null)
            {
                RemoveEntryPortal(instance);
                return;
            }

            var targetPosition = instance.Template.GateCenterPosition ?? new Position(11100, 6500);
            if (instance.EntryPortal != null)
            {
                var sameMap = instance.EntryPortal.Spacemap != null && instance.EntryPortal.Spacemap.Id == map.Id;
                var samePosition = instance.EntryPortal.Position != null &&
                                   instance.EntryPortal.Position.X == position.X &&
                                   instance.EntryPortal.Position.Y == position.Y;

                if (sameMap && samePosition)
                    return;

                instance.EntryPortal.Remove();
                instance.EntryPortal = null;
            }

            instance.EntryPortal = new GalaxyGateEntryPortal(
                this,
                instance.Template,
                map,
                position,
                targetPosition,
                instance.Template.EntryPortalGraphicId,
                instance.OwnerFactionId,
                instance.OwnerId);
        }

        private void RemoveEntryPortal(GalaxyGateInstance instance)
        {
            if (instance?.EntryPortal == null)
                return;

            instance.EntryPortal.Remove();
            instance.EntryPortal = null;
        }

        private void GetEntryMapForFaction(GalaxyGateTemplate template, int factionId, out int mapId, out Position position)
        {
            var defaultMap = template.EntryMapId;
            var defaultPos = template.EntryPortalPosition ?? new Position(11100, 6500);

            if (factionId == 1)
            {
                mapId = template.EntryMapIdMmo > 0 ? template.EntryMapIdMmo : defaultMap;
                position = template.EntryPortalPositionMmo ?? defaultPos;
                return;
            }

            if (factionId == 2)
            {
                mapId = template.EntryMapIdEic > 0 ? template.EntryMapIdEic : defaultMap;
                position = template.EntryPortalPositionEic ?? defaultPos;
                return;
            }

            if (factionId == 3)
            {
                mapId = template.EntryMapIdVru > 0 ? template.EntryMapIdVru : defaultMap;
                position = template.EntryPortalPositionVru ?? defaultPos;
                return;
            }

            mapId = defaultMap;
            position = defaultPos;
        }

        public async void Enter(Player player, GalaxyGateTemplate template, int sourcePortalId)
        {
            if (player == null || template == null || player.Storage.Jumping)
                return;

            GalaxyGateInstance instance;
            lock (gateLock)
                instance = GetOrCreateInstance(player, template);

            if (instance == null)
                return;

            if (instance.Failed)
            {
                player.SendPacket("0|A|STD|Your gate failed (no lives left).");
                return;
            }

            if (instance.Completed)
            {
                player.SendPacket("0|A|STD|This gate is already completed.");
                return;
            }

            if (instance.OwnerFactionId != player.FactionId)
            {
                instance.OwnerFactionId = player.FactionId;
                instance.PendingPersist = true;
                EnsureEntryPortal(instance);
            }

            player.Storage.ActiveGalaxyGateInstanceId = instance.Id;
            await JumpPlayer(player, instance.MapId, instance.Template.GateCenterPosition, sourcePortalId);
            PreparePlayerReturn(instance);
        }

        public void HandlePlayerDestroyed(Player player)
        {
            if (player == null || player.Storage.ActiveGalaxyGateInstanceId <= 0)
                return;

            GalaxyGateInstance instance;
            lock (gateLock)
            {
                if (!instancesById.TryGetValue(player.Storage.ActiveGalaxyGateInstanceId, out instance))
                    return;
            }

            if (instance.OwnerId != player.Id)
                return;

            instance.LivesLeft = Math.Max(0, instance.LivesLeft - 1);
            instance.PendingPersist = true;
            player.SendPacket($"0|A|STD|Galaxy Gate lives left: {instance.LivesLeft}");

            if (instance.LivesLeft > 0)
                return;

            instance.Failed = true;
            instance.PendingPersist = true;
            RemoveEntryPortal(instance);
            player.SendPacket("0|A|STD|Galaxy Gate failed. You have no lives left.");
        }

        public void HandleNpcDestroyed(InstanceNpc npc)
        {
            if (npc == null)
                return;

            GalaxyGateInstance instance = null;
            lock (gateLock)
                instance = instancesById.Values.FirstOrDefault(x => x.MapId == npc.Spacemap.Id);

            if (instance == null)
                return;

            string slotKey;
            lock (gateLock)
            {
                if (!instance.RuntimeNpcToSlot.TryGetValue(npc.Id, out slotKey))
                    return;

                instance.RuntimeNpcToSlot.Remove(npc.Id);
                instance.DestroyedNpcSlots.Add(slotKey);
                instance.PendingPersist = true;
            }

            lock (instance.Spacemap.InstanceNpcs)
                instance.Spacemap.InstanceNpcs.Remove(npc);

            if (IsCurrentWaveCompleted(instance))
                EnsureDecisionPortals(instance);
        }

        public async void AdvanceWave(Player player, int instanceId, int sourcePortalId)
        {
            var instance = GetOwnedInstance(player, instanceId);
            if (instance == null || instance.Completed || instance.Failed)
                return;

            if (!IsCurrentWaveCompleted(instance))
                return;

            RemoveDecisionPortals(instance);

            var nextWave = instance.CurrentWave + 1;
            instance.CurrentWave = nextWave;

            if (nextWave > instance.Template.Waves.Count)
            {
                instance.Completed = true;
                instance.PendingPersist = true;
                RemoveEntryPortal(instance);
                player.SendPacket("0|A|STD|Galaxy Gate completed.");
                await ExitToBase(player, instanceId, sourcePortalId);
                return;
            }

            var previousMap = instance.Spacemap;
            AllocateFreshInstanceMap(instance);

            instance.PendingPersist = true;
            await JumpPlayer(player, instance.MapId, GetGateCenterByFaction(instance.Template, instance.OwnerFactionId), sourcePortalId);
            DestroyInstanceMap(previousMap);
            SpawnCurrentWave(instance);
        }

        public async Task ExitToBase(Player player, int instanceId, int sourcePortalId)
        {
            var instance = GetOwnedInstance(player, instanceId);
            if (instance == null)
                return;

            var baseMap = instance.Template.BaseMapId > 0 ? instance.Template.BaseMapId : player.GetBaseMapId();
            var basePosition = instance.Template.BasePosition ?? player.GetBasePosition();
            player.Storage.ActiveGalaxyGateInstanceId = 0;

            if (instance.PendingPersist)
            {
                GalaxyGateRepository.SaveInstance(instance);
                instance.PendingPersist = false;
            }

            await JumpPlayer(player, baseMap, basePosition, sourcePortalId);
            EnsureEntryPortal(instance);
        }

        public bool IsGalaxyGateMap(int mapId)
        {
            lock (gateLock)
                return instancesById.Values.Any(x => x.MapId == mapId);
        }

        public int GetClientMapId(int internalMapId)
        {
            lock (gateLock)
            {
                var instance = instancesById.Values.FirstOrDefault(x => x.MapId == internalMapId);
                if (instance?.Template != null && instance.Template.VisualMapId > 0)
                    return instance.Template.VisualMapId;
            }

            return internalMapId;
        }

        private GalaxyGateInstance GetOwnedInstance(Player player, int instanceId)
        {
            if (player == null)
                return null;

            lock (gateLock)
            {
                GalaxyGateInstance instance;
                if (!instancesById.TryGetValue(instanceId, out instance))
                    return null;
                if (instance.OwnerId != player.Id)
                    return null;
                return instance;
            }
        }

        private GalaxyGateInstance GetOrCreateInstance(Player player, GalaxyGateTemplate template)
        {
            int existingId;
            if (ownerToInstance.TryGetValue(player.Id, out existingId))
            {
                GalaxyGateInstance loaded;
                if (instancesById.TryGetValue(existingId, out loaded))
                {
                    if (loaded.TemplateId == template.Id)
                    {
                        EnsureInstanceMap(loaded);
                        EnsureEntryPortal(loaded);
                        return loaded;
                    }
                }
            }

            var persisted = GalaxyGateRepository.LoadPlayerInstance(player.Id, template.Id);
            if (persisted != null)
            {
                persisted.Template = template;
                EnsureInstanceMap(persisted);
                instancesById[persisted.Id] = persisted;
                ownerToInstance[player.Id] = persisted.Id;
                EnsureEntryPortal(persisted);
                return persisted;
            }

            var instance = new GalaxyGateInstance
            {
                OwnerId = player.Id,
                OwnerFactionId = player.FactionId,
                TemplateId = template.Id,
                Template = template,
                CurrentWave = 1,
                LivesLeft = template.MaxLives <= 0 ? 5 : template.MaxLives,
                Completed = false,
                Failed = false,
                MapId = template.VisualMapId > 0 ? AllocateDynamicMapId(template.VisualMapId) : AllocateDynamicMapId()
            };

            EnsureInstanceMap(instance);
            instance.Id = GalaxyGateRepository.InsertInstance(instance);
            instancesById[instance.Id] = instance;
            ownerToInstance[player.Id] = instance.Id;
            EnsureEntryPortal(instance);
            return instance;
        }

        public bool HasOwnedInstance(int playerId, int templateId)
        {
            lock (gateLock)
            {
                return instancesById.Values.Any(x => x.OwnerId == playerId && x.TemplateId == templateId && !x.Completed && !x.Failed);
            }
        }

        private int AllocateDynamicMapId()
        {
            while (GameManager.GetSpacemap(nextDynamicMapId) != null)
                nextDynamicMapId++;
            return nextDynamicMapId++;
        }

        private int AllocateDynamicMapId(int preferredStart)
        {
            if (preferredStart <= 0)
                return AllocateDynamicMapId();

            var candidate = preferredStart;
            while (GameManager.GetSpacemap(candidate) != null)
                candidate++;
            return candidate;
        }

        private void EnsureInstanceMap(GalaxyGateInstance instance)
        {
            var map = GameManager.GetSpacemap(instance.MapId);
            if (map == null)
            {
                var options = new OptionsBase
                {
                    StarterMap = false,
                    PvpMap = false,
                    RangeDisabled = false,
                    CloakBlocked = true,
                    LogoutBlocked = true,
                    DeathLocationRepair = false
                };

                map = new Spacemap(
                    instance.MapId,
                    $"GG-{instance.Template.Name}-{instance.OwnerId}",
                    0,
                    null,
                    null,
                    null,
                    options,
                    null,
                    instance.Template.VisualMapId,
                    false);

                map.Instance = true;
                map.Curwave = Math.Max(0, instance.CurrentWave - 1);
                GameManager.Spacemaps.TryAdd(map.Id, map);
            }

            instance.Spacemap = map;
        }

        private async Task JumpPlayer(Player player, int mapId, Position targetPosition, int sourcePortalId)
        {
            if (player == null || player.Storage.Jumping)
                return;

            if (targetPosition == null)
                targetPosition = new Position(11100, 6500);

            player.Storage.Jumping = true;
            try
            {
                player.SendCommand(ActivatePortalCommand.write(GetClientMapId(mapId), sourcePortalId));
                await Task.Delay(Portal.JUMP_DELAY);

                var targetMap = GameManager.GetSpacemap(mapId);
                if (targetMap == null)
                {
                    targetMap = GameManager.GetSpacemap(player.GetBaseMapId());
                    targetPosition = player.GetBasePosition();
                    player.SendPacket("0|A|STD|Galaxy Gate map was unavailable. You were returned to base.");
                }

                EnforceGalaxyGateVisibilityRules(player, targetMap);
                player.LastCombatTime = DateTime.Now.AddSeconds(-999);
                player.AllMapRange = false;
                player.Spacemap?.RemoveCharacter(player);
                player.CurrentInRangePortalId = -1;
                player.Deselection();
                player.Storage.InRangeAssets.Clear();
                player.Storage.InRangeObjects.Clear();
                player.InRangeCharacters.Clear();
                player.SetPosition(targetPosition);
                player.Spacemap = targetMap;
                player.Spacemap?.AddAndInitPlayer(player);
                player.AllMapRange = IsGalaxyGateMap(targetMap.Id);
                targetMap.CheckActivatables(player);
            }
            finally
            {
                player.Storage.Jumping = false;
            }
        }

        private void PreparePlayerReturn(GalaxyGateInstance instance)
        {
            if (instance == null || instance.Spacemap == null)
                return;

            if (instance.Completed || instance.Failed)
                return;

            ClearWaveNpcs(instance);

            if (IsCurrentWaveCompleted(instance))
                EnsureDecisionPortals(instance);
            else
            {
                RemoveDecisionPortals(instance);
                SpawnCurrentWave(instance);
            }
        }

        private void SpawnCurrentWave(GalaxyGateInstance instance)
        {
            if (instance.WaveSpawnInProgress)
                return;

            instance.WaveSpawnInProgress = true;

            Task.Run(async () =>
            {
                try
                {
                    var wave = instance.Template.Waves.FirstOrDefault(x => x.Id == instance.CurrentWave);
                    if (wave == null)
                        return;

                    if (!await RunWaveStartCountdown(instance, wave, 15))
                        return;

                    var split = BuildQuarterSizes(wave.NpcCount);
                    var startIndex = 0;

                    for (var quarter = 0; quarter < split.Count; quarter++)
                    {
                        if (instance.Completed || instance.Failed)
                            break;

                        SendSubWaveStartAnnouncement(instance, wave, quarter);
                        SpawnWaveChunk(instance, wave, startIndex, split[quarter], quarter);
                        startIndex += split[quarter];

                        if (quarter < split.Count - 1)
                            await Task.Delay(5000);
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("error_log", $"- [GalaxyGateManager.cs] SpawnCurrentWave exception: {e}");
                }
                finally
                {
                    instance.WaveSpawnInProgress = false;
                }
            });
        }

        private List<int> BuildQuarterSizes(int total)
        {
            var result = new List<int> { 0, 0, 0, 0 };
            if (total <= 0) return result;

            var baseCount = total / 4;
            var remainder = total % 4;

            for (var i = 0; i < 4; i++)
                result[i] = baseCount + (i < remainder ? 1 : 0);

            return result;
        }

        private void SpawnWaveChunk(GalaxyGateInstance instance, GalaxyGateWaveTemplate wave, int startSlot, int amount, int quarter)
        {
            if (amount <= 0)
                return;

            var center = GetGateCenterByFaction(instance.Template, instance.OwnerFactionId);
            var map = instance.Spacemap;
            var suffixPrefix = ResolveNpcSuffixPrefix(instance.Template);
            var subWaveNumber = ((wave.Id - 1) * 4) + (quarter + 1);

            for (var i = 0; i < amount; i++)
            {
                var slot = startSlot + i;
                var slotKey = $"{wave.Id}:{slot}";
                if (instance.DestroyedNpcSlots.Contains(slotKey))
                    continue;

                var ship = GameManager.GetShip(wave.NpcId);
                if (ship == null)
                    continue;

                var position = Position.GetPosOnCircle(center, 3000);
                var suffix = $" ~ {suffixPrefix} {subWaveNumber}";
                var npc = new InstanceNpc(
                    Randoms.CreateRandomID(),
                    ship,
                    map,
                    position,
                    0,
                    wave.Multiplier,
                    suffix,
                    wave.KeyNpc == 1);

                lock (map.InstanceNpcs)
                    map.InstanceNpcs.Add(npc);

                lock (gateLock)
                    instance.RuntimeNpcToSlot[npc.Id] = slotKey;

                if (wave.KeyNpc == 1 && wave.MinionsCount > 0)
                {
                    for (var m = 0; m < wave.MinionsCount; m++)
                    {
                        var minionShip = GameManager.GetShip(wave.MinionsId);
                        if (minionShip == null) break;
                        var escort = new Escort(
                            Randoms.CreateRandomID(),
                            minionShip,
                            map,
                            npc.Position,
                            wave.MinionsMultiplier,
                            suffix,
                            npc);
                        npc.Minions.Add(escort);
                        ForceAggroOnOwner(instance, escort);
                    }
                    npc.Check();
                }

                ForceAggroOnOwner(instance, npc);
            }
        }

        private void SendSubWaveStartAnnouncement(GalaxyGateInstance instance, GalaxyGateWaveTemplate wave, int quarter)
        {
            if (instance == null || wave == null)
                return;

            var owner = GameManager.GetPlayerById(instance.OwnerId);
            if (owner == null || owner.Spacemap == null || owner.Spacemap.Id != instance.MapId)
                return;

            var gateName = string.IsNullOrWhiteSpace(instance.Template?.Name) ? "GG" : instance.Template.Name;
            var subWaveNumber = ((wave.Id - 1) * 4) + (quarter + 1);
            owner.SendPacket($"0|A|STD|{gateName} - Wave {subWaveNumber} starting");
        }

        private static string NormalizeGateDisplayName(string value)
        {
            var normalized = (value ?? "").Trim();
            if (normalized.Length == 0)
                return "";

            switch (normalized.ToUpperInvariant())
            {
                case "\u0391":
                case "A":
                case "ALPHA":
                case "Î±":
                case "ÃŽÂ±":
                    return "ALPHA";
                case "\u0392":
                case "B":
                case "BETA":
                case "Î²":
                case "ÃŽÂ²":
                    return "BETA";
                case "\u0393":
                case "G":
                case "GAMMA":
                case "Î³":
                case "ÃŽÂ³":
                    return "GAMMA";
                case "\u0394":
                case "D":
                case "DELTA":
                case "Î´":
                case "ÃŽÂ´":
                    return "DELTA";
                default:
                    return normalized.ToUpperInvariant();
            }
        }

        private static string GetDisplayGateName(string value)
        {
            switch (NormalizeGateDisplayName(value))
            {
                case "ALPHA":
                    return "\u03B1";
                case "BETA":
                    return "\u03B2";
                case "GAMMA":
                    return "\u03B3";
                case "DELTA":
                    return "\u03B4";
                default:
                    return (value ?? "").Trim();
            }
        }

        private static string ResolveNpcSuffixPrefix(GalaxyGateTemplate template)
        {
            if (template == null)
                return "GG";

            var configuredSuffix = (template.NpcSuffix ?? "").Trim();
            var normalizedConfiguredSuffix = NormalizeGateDisplayName(configuredSuffix);
            var normalizedTemplateCode = NormalizeGateDisplayName(template.CodeName);

            if (normalizedTemplateCode == "ALPHA" || normalizedTemplateCode == "BETA" || normalizedTemplateCode == "GAMMA" || normalizedTemplateCode == "DELTA")
            {
                if (configuredSuffix.Length == 0 ||
                    normalizedConfiguredSuffix == "ALPHA" ||
                    normalizedConfiguredSuffix == "BETA" ||
                    normalizedConfiguredSuffix == "GAMMA" ||
                    normalizedConfiguredSuffix == "DELTA")
                    return GetDisplayGateName(template.CodeName);
            }

            var fallback = configuredSuffix.Length > 0 ? configuredSuffix : template.Name;
            return GetDisplayGateName(fallback);
        }

        private async Task<bool> RunWaveStartCountdown(GalaxyGateInstance instance, GalaxyGateWaveTemplate wave, int seconds)
        {
            if (instance == null || wave == null || seconds <= 0)
                return true;

            var owner = GameManager.GetPlayerById(instance.OwnerId);
            if (owner == null || owner.Spacemap == null || owner.Spacemap.Id != instance.MapId)
                return true;

            for (var i = seconds; i > 0; i--)
            {
                if (instance.Completed || instance.Failed)
                    return false;

                if (owner.Spacemap == null || owner.Spacemap.Id != instance.MapId)
                    return false;

                owner.SendPacket($"0|A|STD|-=-{i}-=-");
                await Task.Delay(1000);
            }

            owner.SendPacket($"0|A|STD|Lives remaining: {instance.LivesLeft}");

            var firstSubwave = ((wave.Id - 1) * 4) + 1;
            return true;
        }

        private void ForceAggroOnOwner(GalaxyGateInstance instance, Npc npc)
        {
            if (instance == null || npc == null)
                return;

            var owner = GameManager.GetPlayerById(instance.OwnerId);
            if (owner == null || owner.Spacemap == null || owner.Spacemap.Id != instance.MapId)
                return;

            npc.ReceiveAttack(owner);
        }

        private bool IsCurrentWaveCompleted(GalaxyGateInstance instance)
        {
            var wave = instance.Template.Waves.FirstOrDefault(x => x.Id == instance.CurrentWave);
            if (wave == null || wave.NpcCount <= 0)
                return true;

            for (var i = 0; i < wave.NpcCount; i++)
            {
                var key = $"{wave.Id}:{i}";
                if (!instance.DestroyedNpcSlots.Contains(key))
                    return false;
            }
            return true;
        }

        private void EnsureDecisionPortals(GalaxyGateInstance instance)
        {
            CleanupStaleDecisionPortals(instance);
            if (instance.TemporaryPortals.Count >= 2)
                return;

            RemoveDecisionPortals(instance);

            var center = GetGateCenterByFaction(instance.Template, instance.OwnerFactionId);
            var leftPos = new Position(center.X - 600, center.Y);
            var rightPos = new Position(center.X + 600, center.Y);
            var defaultGraphic = instance.Template.EntryPortalGraphicId > 0 ? instance.Template.EntryPortalGraphicId : 41;
            var waveGraphic = instance.Template.WavePortalGraphicId > 0 ? instance.Template.WavePortalGraphicId : defaultGraphic;
            var exitGraphic = instance.Template.ExitPortalGraphicId > 0 ? instance.Template.ExitPortalGraphicId : defaultGraphic;

            var nextPortal = new GalaxyGateAdvancePortal(
                this,
                instance,
                instance.Spacemap,
                leftPos,
                leftPos,
                waveGraphic);

            var exitPortal = new GalaxyGateExitPortal(
                this,
                instance,
                instance.Spacemap,
                rightPos,
                rightPos,
                exitGraphic);

            instance.TemporaryPortals.Add(nextPortal);
            instance.TemporaryPortals.Add(exitPortal);

            GameManager.SendCommandToMap(instance.Spacemap.Id, nextPortal.GetAssetCreateCommand());
            GameManager.SendCommandToMap(instance.Spacemap.Id, exitPortal.GetAssetCreateCommand());
        }

        private void RemoveDecisionPortals(GalaxyGateInstance instance)
        {
            foreach (var portal in instance.TemporaryPortals.ToList())
                portal?.Remove();
            instance.TemporaryPortals.Clear();
        }

        private Position ResolveDecisionPortalTargetPosition(GalaxyGateInstance instance, int portalId)
        {
            if (instance == null || portalId <= 0 || instance.TemporaryPortals == null)
                return null;

            var portal = instance.TemporaryPortals.FirstOrDefault(x => x != null && x.Id == portalId);
            return portal?.TargetPosition;
        }

        private void ClearWaveNpcs(GalaxyGateInstance instance)
        {
            if (instance.Spacemap == null)
                return;

            lock (instance.Spacemap.InstanceNpcs)
            {
                foreach (var npc in instance.Spacemap.InstanceNpcs.ToList())
                {
                    npc.Destroyed = true;
                    instance.Spacemap.RemoveCharacter(npc);
                }
                instance.Spacemap.InstanceNpcs.Clear();
            }

            lock (gateLock)
                instance.RuntimeNpcToSlot.Clear();
        }

        private void CleanupStaleDecisionPortals(GalaxyGateInstance instance)
        {
            if (instance?.TemporaryPortals == null || instance.Spacemap == null)
                return;

            var stalePortals = instance.TemporaryPortals
                .Where(x => x == null ||
                            x.Spacemap == null ||
                            x.Spacemap.Id != instance.Spacemap.Id ||
                            !instance.Spacemap.Activatables.ContainsKey(x.Id))
                .ToList();

            foreach (var portal in stalePortals)
                instance.TemporaryPortals.Remove(portal);
        }

        private void AllocateFreshInstanceMap(GalaxyGateInstance instance)
        {
            if (instance == null)
                return;

            instance.MapId = instance.Template != null && instance.Template.VisualMapId > 0
                ? AllocateDynamicMapId(instance.Template.VisualMapId)
                : AllocateDynamicMapId();

            instance.Spacemap = null;
            EnsureInstanceMap(instance);
        }

        private void DestroyInstanceMap(Spacemap map)
        {
            if (map == null || !map.Instance)
                return;

            foreach (var portal in map.Activatables.Values.OfType<Portal>().ToList())
                portal.Remove();
            map.Activatables.Clear();

            foreach (var obj in map.Objects.Values.ToList())
            {
                if (obj is Collectable collectable)
                {
                    collectable.Respawnable = false;
                    collectable.Dispose();
                }
                else if (obj is Mine mine)
                {
                    mine.Remove(true);
                }
                else if (obj is Asset asset)
                {
                    asset.Remove();
                }
                else
                {
                    map.Objects.TryRemove(obj.Id, out var removedObject);
                }
            }

            foreach (var character in map.Characters.Values.ToList())
            {
                if (character is Player)
                    continue;

                character.Destroyed = true;
                character.Deselection();
                character.InRangeCharacters.Clear();
                map.RemoveCharacter(character);

                if (character is Tick tickable)
                    Program.TickManager.RemoveTick(tickable);
            }

            map.InstanceNpcs.Clear();
            map.POIs.Clear();
            Program.TickManager.RemoveTick(map);
            GameManager.Spacemaps.TryRemove(map.Id, out var removedMap);
        }

        private void EnforceGalaxyGateVisibilityRules(Player player, Spacemap targetMap)
        {
            if (player == null)
                return;

            var currentMapIsGate = player.Spacemap != null && IsGalaxyGateMap(player.Spacemap.Id);
            var targetMapIsGate = targetMap != null && IsGalaxyGateMap(targetMap.Id);
            if (!currentMapIsGate && !targetMapIsGate)
                return;

            if (player.Invisible)
            {
                player.Invisible = false;
                player.UpdateShipStatus();
                var cloakPacket = $"0|n|INV|{player.Id}|0";
                player.SendPacket(cloakPacket);
                player.SendPacketToInRangePlayers(cloakPacket);
                player.SettingsManager?.SendNewItemStatus(CpuManager.CLK_XL);
            }

            if (player.Pet != null && player.Pet.Activated && player.Pet.Invisible)
            {
                player.Pet.Invisible = false;
                player.Pet.SendPacketToInRangePlayers($"0|n|INV|{player.Pet.Id}|0");
            }
        }

        private Position GetGateCenterByFaction(GalaxyGateTemplate template, int factionId)
        {
            if (template == null)
                return new Position(11100, 6500);

            if (factionId == 1 && template.GateCenterPositionMmo != null)
                return template.GateCenterPositionMmo;
            if (factionId == 2 && template.GateCenterPositionEic != null)
                return template.GateCenterPositionEic;
            if (factionId == 3 && template.GateCenterPositionVru != null)
                return template.GateCenterPositionVru;

            return template.GateCenterPosition ?? new Position(11100, 6500);
        }
    }

    class GalaxyGateEntryPortal : Portal
    {
        private readonly GalaxyGateManager manager;
        private readonly GalaxyGateTemplate template;
        private readonly int factionId;
        private readonly int ownerId;

        public GalaxyGateEntryPortal(GalaxyGateManager manager, GalaxyGateTemplate template, Spacemap spacemap, Position position, Position targetPosition, int graphicsId, int factionId, int ownerId = 0)
            : base(spacemap, position, targetPosition, template.EntryMapId, graphicsId, factionId, true, true, false)
        {
            this.manager = manager;
            this.template = template;
            this.factionId = factionId;
            this.ownerId = ownerId;
        }

        public override bool IsVisibleTo(Player player)
        {
            if (player == null)
                return false;

            if (ownerId > 0)
            {
                if (player.Id != ownerId)
                    return false;
            }
            else if (manager.HasOwnedInstance(player.Id, template.Id))
            {
                return false;
            }

            return factionId <= 0 || player.FactionId == factionId;
        }

        public override bool CanInteract(Player player)
        {
            return IsVisibleTo(player);
        }

        public override void Click(GameSession gameSession)
        {
            if (gameSession?.Player == null) return;
            if (!CanInteract(gameSession.Player)) return;
            manager.Enter(gameSession.Player, template, Id);
        }
    }

    class GalaxyGateAdvancePortal : Portal
    {
        private readonly GalaxyGateManager manager;
        private readonly GalaxyGateInstance instance;

        public GalaxyGateAdvancePortal(GalaxyGateManager manager, GalaxyGateInstance instance, Spacemap spacemap, Position position, Position targetPosition, int graphicsId)
            : base(spacemap, position, targetPosition, spacemap.Id, graphicsId, 0, true, true, false)
        {
            this.manager = manager;
            this.instance = instance;
        }

        public override void Click(GameSession gameSession)
        {
            if (gameSession?.Player == null) return;
            manager.AdvanceWave(gameSession.Player, instance.Id, Id);
        }
    }

    class GalaxyGateExitPortal : Portal
    {
        private readonly GalaxyGateManager manager;
        private readonly GalaxyGateInstance instance;

        public GalaxyGateExitPortal(GalaxyGateManager manager, GalaxyGateInstance instance, Spacemap spacemap, Position position, Position targetPosition, int graphicsId)
            : base(spacemap, position, targetPosition, spacemap.Id, graphicsId, 0, true, true, false)
        {
            this.manager = manager;
            this.instance = instance;
        }

        public override async void Click(GameSession gameSession)
        {
            if (gameSession?.Player == null) return;
            await manager.ExitToBase(gameSession.Player, instance.Id, Id);
        }
    }

    static class GalaxyGateRepository
    {
        public static void EnsureSchema()
        {
            using (var sql = SqlDatabaseManager.GetClient())
            {
                sql.ExecuteNonQuery(
                    "CREATE TABLE IF NOT EXISTS server_galaxy_gate_templates (" +
                    "id INT NOT NULL PRIMARY KEY AUTO_INCREMENT," +
                    "name VARCHAR(64) NOT NULL," +
                    "entry_map_id INT NOT NULL," +
                    "visual_map_id INT NOT NULL DEFAULT 0," +
                    "entry_x INT NOT NULL DEFAULT 11100," +
                    "entry_y INT NOT NULL DEFAULT 6500," +
                    "entry_map_id_mmo INT NOT NULL DEFAULT 0," +
                    "entry_x_mmo INT NOT NULL DEFAULT 0," +
                    "entry_y_mmo INT NOT NULL DEFAULT 0," +
                    "entry_map_id_eic INT NOT NULL DEFAULT 0," +
                    "entry_x_eic INT NOT NULL DEFAULT 0," +
                    "entry_y_eic INT NOT NULL DEFAULT 0," +
                    "entry_map_id_vru INT NOT NULL DEFAULT 0," +
                    "entry_x_vru INT NOT NULL DEFAULT 0," +
                    "entry_y_vru INT NOT NULL DEFAULT 0," +
                    "entry_graphic_id INT NOT NULL DEFAULT 41," +
                    "base_map_id INT NOT NULL DEFAULT 1," +
                    "base_x INT NOT NULL DEFAULT 1600," +
                    "base_y INT NOT NULL DEFAULT 1600," +
                    "wave_portal_graphic_id INT NOT NULL DEFAULT 41," +
                    "exit_portal_graphic_id INT NOT NULL DEFAULT 41," +
                    "center_x INT NOT NULL DEFAULT 11100," +
                    "center_y INT NOT NULL DEFAULT 6500," +
                    "center_x_mmo INT NOT NULL DEFAULT 11100," +
                    "center_y_mmo INT NOT NULL DEFAULT 6500," +
                    "center_x_eic INT NOT NULL DEFAULT 11100," +
                    "center_y_eic INT NOT NULL DEFAULT 6500," +
                    "center_x_vru INT NOT NULL DEFAULT 11100," +
                    "center_y_vru INT NOT NULL DEFAULT 6500," +
                    "npc_suffix VARCHAR(32) NOT NULL DEFAULT 'GG'," +
                    "max_lives INT NOT NULL DEFAULT 5" +
                    ")");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS visual_map_id INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS gate_map_id INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("UPDATE server_galaxy_gate_templates SET visual_map_id = gate_map_id WHERE visual_map_id = 0");

                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS entry_map_id_mmo INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS entry_x_mmo INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS entry_y_mmo INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS entry_map_id_eic INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS entry_x_eic INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS entry_y_eic INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS entry_map_id_vru INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS entry_x_vru INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS entry_y_vru INT NOT NULL DEFAULT 0");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS center_x_mmo INT NOT NULL DEFAULT 11100");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS center_y_mmo INT NOT NULL DEFAULT 6500");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS center_x_eic INT NOT NULL DEFAULT 11100");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS center_y_eic INT NOT NULL DEFAULT 6500");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS center_x_vru INT NOT NULL DEFAULT 11100");
                sql.ExecuteNonQuery("ALTER TABLE server_galaxy_gate_templates ADD COLUMN IF NOT EXISTS center_y_vru INT NOT NULL DEFAULT 6500");

                sql.ExecuteNonQuery(
                    "CREATE TABLE IF NOT EXISTS server_galaxy_gate_waves (" +
                    "id INT NOT NULL PRIMARY KEY AUTO_INCREMENT," +
                    "gate_id INT NOT NULL," +
                    "wave_id INT NOT NULL," +
                    "npc_id INT NOT NULL," +
                    "npc_count INT NOT NULL," +
                    "multiplier INT NOT NULL DEFAULT 1," +
                    "key_npc INT NOT NULL DEFAULT 0," +
                    "minions_id INT NOT NULL DEFAULT 0," +
                    "minions_count INT NOT NULL DEFAULT 0," +
                    "minions_multiplier INT NOT NULL DEFAULT 1," +
                    "UNIQUE KEY uq_gate_wave (gate_id, wave_id)" +
                    ")");

                sql.ExecuteNonQuery(
                    "CREATE TABLE IF NOT EXISTS player_galaxy_gate_instances (" +
                    "id INT NOT NULL PRIMARY KEY AUTO_INCREMENT," +
                    "player_id INT NOT NULL," +
                    "owner_faction_id INT NOT NULL DEFAULT 0," +
                    "template_id INT NOT NULL," +
                    "map_id INT NOT NULL," +
                    "current_wave INT NOT NULL DEFAULT 1," +
                    "lives_left INT NOT NULL DEFAULT 5," +
                    "is_completed TINYINT(1) NOT NULL DEFAULT 0," +
                    "is_failed TINYINT(1) NOT NULL DEFAULT 0," +
                    "destroyed_npcs_json LONGTEXT NULL," +
                    "updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP," +
                    "created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP," +
                    "UNIQUE KEY uq_player_template (player_id, template_id)" +
                    ")");
                sql.ExecuteNonQuery("ALTER TABLE player_galaxy_gate_instances ADD COLUMN IF NOT EXISTS owner_faction_id INT NOT NULL DEFAULT 0");
            }
        }

        public static List<GalaxyGateTemplate> LoadTemplates()
        {
            var result = new List<GalaxyGateTemplate>();
            using (var sql = SqlDatabaseManager.GetClient())
            {
                var hasInstanceWavesTable = TableExists(sql, "server_instanceswaves");
                var hasLegacyWavesTable = TableExists(sql, "server_galaxy_gate_waves");
                var table = sql.ExecuteQueryTable("SELECT * FROM server_galaxy_gate_templates ORDER BY id ASC");
                if (table == null)
                    return result;

                foreach (DataRow row in table.Rows)
                {
                    var template = new GalaxyGateTemplate
                    {
                        Id = Convert.ToInt32(row["id"]),
                        Name = (Convert.ToString(row["name"]) == "ALPHA") ? "α" : Convert.ToString(row["name"]) == "BETA" ? "β" : Convert.ToString(row["name"]) == "GAMMA" ? "γ" : Convert.ToString(row["name"]) == "DELTA" ? "δ" : Convert.ToString(row["name"]),
                        EntryMapId = Convert.ToInt32(row["entry_map_id"]),
                        VisualMapId = GetInt(row, "visual_map_id", GetInt(row, "gate_map_id", 0)),
                        EntryPortalPosition = new Position(Convert.ToInt32(row["entry_x"]), Convert.ToInt32(row["entry_y"])),
                        EntryMapIdMmo = GetInt(row, "entry_map_id_mmo", 0),
                        EntryMapIdEic = GetInt(row, "entry_map_id_eic", 0),
                        EntryMapIdVru = GetInt(row, "entry_map_id_vru", 0),
                        EntryPortalPositionMmo = new Position(GetInt(row, "entry_x_mmo", 0), GetInt(row, "entry_y_mmo", 0)),
                        EntryPortalPositionEic = new Position(GetInt(row, "entry_x_eic", 0), GetInt(row, "entry_y_eic", 0)),
                        EntryPortalPositionVru = new Position(GetInt(row, "entry_x_vru", 0), GetInt(row, "entry_y_vru", 0)),
                        EntryPortalGraphicId = Convert.ToInt32(row["entry_graphic_id"]),
                        BaseMapId = Convert.ToInt32(row["base_map_id"]),
                        BasePosition = new Position(Convert.ToInt32(row["base_x"]), Convert.ToInt32(row["base_y"])),
                        WavePortalGraphicId = Convert.ToInt32(row["wave_portal_graphic_id"]),
                        ExitPortalGraphicId = Convert.ToInt32(row["exit_portal_graphic_id"]),
                        GateCenterPosition = new Position(Convert.ToInt32(row["center_x"]), Convert.ToInt32(row["center_y"])),
                        GateCenterPositionMmo = new Position(GetInt(row, "center_x_mmo", 11100), GetInt(row, "center_y_mmo", 6500)),
                        GateCenterPositionEic = new Position(GetInt(row, "center_x_eic", 11100), GetInt(row, "center_y_eic", 6500)),
                        GateCenterPositionVru = new Position(GetInt(row, "center_x_vru", 11100), GetInt(row, "center_y_vru", 6500)),
                        NpcSuffix = Convert.ToString(row["npc_suffix"]),
                        MaxLives = Convert.ToInt32(row["max_lives"])
                    };

                    template.CodeName = NormalizeGateTemplateName(Convert.ToString(row["name"]));
                    template.Name = GetDisplayGateName(Convert.ToString(row["name"]));

                    DataTable waves = null;
                    if (hasInstanceWavesTable)
                    {
                        var preferredGateId = ResolveLegacyGateId(template.CodeName);
                        var candidateGateIds = new List<int>();

                        if (preferredGateId > 0)
                            candidateGateIds.Add(preferredGateId);
                        if (!candidateGateIds.Contains(template.Id))
                            candidateGateIds.Add(template.Id);

                        foreach (var gateId in candidateGateIds)
                        {
                            waves = sql.ExecuteQueryTable($"SELECT * FROM server_instanceswaves WHERE GateID = {gateId} ORDER BY WaveID ASC");
                            if (waves != null && waves.Rows.Count > 0)
                                break;
                        }
                    }

                    if ((waves == null || waves.Rows.Count == 0) && hasLegacyWavesTable)
                        waves = sql.ExecuteQueryTable($"SELECT * FROM server_galaxy_gate_waves WHERE gate_id = {template.Id} ORDER BY wave_id ASC");

                    if (waves != null && waves.Rows.Count > 0)
                    {
                        foreach (DataRow waveRow in waves.Rows)
                            template.Waves.Add(ReadWaveTemplate(waveRow));
                    }

                    result.Add(template);
                }
            }
            return result;
        }

        public static GalaxyGateInstance LoadPlayerInstance(int playerId, int templateId)
        {
            using (var sql = SqlDatabaseManager.GetClient())
            {
                var row = sql.ExecuteQueryRow($"SELECT * FROM player_galaxy_gate_instances WHERE player_id = {playerId} AND template_id = {templateId} LIMIT 1");
                if (row == null)
                    return null;

                var destroyedJson = row["destroyed_npcs_json"] == DBNull.Value ? "[]" : Convert.ToString(row["destroyed_npcs_json"]);
                var destroyed = new List<string>();

                try { destroyed = JsonConvert.DeserializeObject<List<string>>(destroyedJson) ?? new List<string>(); }
                catch { destroyed = new List<string>(); }

                return new GalaxyGateInstance
                {
                    Id = Convert.ToInt32(row["id"]),
                    OwnerId = Convert.ToInt32(row["player_id"]),
                    OwnerFactionId = GetInt(row, "owner_faction_id", 0),
                    TemplateId = Convert.ToInt32(row["template_id"]),
                    MapId = Convert.ToInt32(row["map_id"]),
                    CurrentWave = Convert.ToInt32(row["current_wave"]),
                    LivesLeft = Convert.ToInt32(row["lives_left"]),
                    Completed = Convert.ToBoolean(Convert.ToInt32(row["is_completed"])),
                    Failed = Convert.ToBoolean(Convert.ToInt32(row["is_failed"])),
                    DestroyedNpcSlots = new HashSet<string>(destroyed)
                };
            }
        }

        public static List<GalaxyGateInstance> LoadActiveInstances(Dictionary<int, GalaxyGateTemplate> templatesById)
        {
            var result = new List<GalaxyGateInstance>();
            if (templatesById == null || templatesById.Count == 0)
                return result;

            using (var sql = SqlDatabaseManager.GetClient())
            {
                var table = sql.ExecuteQueryTable("SELECT * FROM player_galaxy_gate_instances WHERE is_completed = 0 AND is_failed = 0 ORDER BY updated_at DESC");
                if (table == null)
                    return result;

                foreach (DataRow row in table.Rows)
                {
                    var templateId = Convert.ToInt32(row["template_id"]);
                    GalaxyGateTemplate template;
                    if (!templatesById.TryGetValue(templateId, out template))
                        continue;

                    var destroyedJson = row["destroyed_npcs_json"] == DBNull.Value ? "[]" : Convert.ToString(row["destroyed_npcs_json"]);
                    var destroyed = new List<string>();

                    try { destroyed = JsonConvert.DeserializeObject<List<string>>(destroyedJson) ?? new List<string>(); }
                    catch { destroyed = new List<string>(); }

                    result.Add(new GalaxyGateInstance
                    {
                        Id = Convert.ToInt32(row["id"]),
                        OwnerId = Convert.ToInt32(row["player_id"]),
                        OwnerFactionId = GetInt(row, "owner_faction_id", 0),
                        TemplateId = templateId,
                        Template = template,
                        MapId = Convert.ToInt32(row["map_id"]),
                        CurrentWave = Convert.ToInt32(row["current_wave"]),
                        LivesLeft = Convert.ToInt32(row["lives_left"]),
                        Completed = false,
                        Failed = false,
                        DestroyedNpcSlots = new HashSet<string>(destroyed)
                    });
                }
            }

            return result;
        }

        public static int InsertInstance(GalaxyGateInstance instance)
        {
            using (var sql = SqlDatabaseManager.GetClient())
            {
                var json = JsonConvert.SerializeObject(instance.DestroyedNpcSlots.ToList()).Replace("'", "''");
                sql.ExecuteNonQuery(
                    $"INSERT INTO player_galaxy_gate_instances " +
                    $"(player_id, owner_faction_id, template_id, map_id, current_wave, lives_left, is_completed, is_failed, destroyed_npcs_json) VALUES " +
                    $"({instance.OwnerId}, {instance.OwnerFactionId}, {instance.TemplateId}, {instance.MapId}, {instance.CurrentWave}, {instance.LivesLeft}, {(instance.Completed ? 1 : 0)}, {(instance.Failed ? 1 : 0)}, '{json}')");

                var row = sql.ExecuteQueryRow("SELECT LAST_INSERT_ID() AS id");
                return row == null ? 0 : Convert.ToInt32(row["id"]);
            }
        }

        public static void SaveInstance(GalaxyGateInstance instance)
        {
            using (var sql = SqlDatabaseManager.GetClient())
            {
                var json = JsonConvert.SerializeObject(instance.DestroyedNpcSlots.ToList()).Replace("'", "''");
                sql.ExecuteNonQuery(
                    $"UPDATE player_galaxy_gate_instances SET " +
                    $"owner_faction_id = {instance.OwnerFactionId}, " +
                    $"map_id = {instance.MapId}, " +
                    $"current_wave = {instance.CurrentWave}, " +
                    $"lives_left = {instance.LivesLeft}, " +
                    $"is_completed = {(instance.Completed ? 1 : 0)}, " +
                    $"is_failed = {(instance.Failed ? 1 : 0)}, " +
                    $"destroyed_npcs_json = '{json}' " +
                    $"WHERE id = {instance.Id}");
            }
        }

        private static bool TableExists(SqlDatabaseClient sql, string tableName)
        {
            var safeName = tableName.Replace("'", "''");
            var row = sql.ExecuteQueryRow($"SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{safeName}' LIMIT 1");
            return row != null;
        }

        private static int ResolveLegacyGateId(string templateName)
        {
            var normalized = NormalizeGateTemplateName(templateName);
            if (normalized == "ALPHA") return 2;
            if (normalized == "BETA") return 3;
            if (normalized == "GAMMA") return 4;
            if (normalized == "DELTA") return 5;
            if (normalized == "EPSILON") return 6;
            if (normalized == "ZETA") return 7;
            if (normalized == "KAPPA") return 8;
            if (normalized == "LAMBDA") return 9;
            if (normalized == "KRONOS") return 10;
            return 0;
        }

        private static string NormalizeGateTemplateName(string value)
        {
            var normalized = (value ?? "").Trim();
            if (normalized.Length == 0)
                return "";

            switch (normalized.ToUpperInvariant())
            {
                case "Α":
                case "A":
                case "ALPHA":
                case "Î±":
                    return "ALPHA";
                case "Β":
                case "B":
                case "BETA":
                case "Î²":
                    return "BETA";
                case "Γ":
                case "G":
                case "GAMMA":
                case "Î³":
                    return "GAMMA";
                case "Δ":
                case "D":
                case "DELTA":
                case "Î´":
                    return "DELTA";
                default:
                    return normalized.ToUpperInvariant();
            }
        }

        private static string GetDisplayGateName(string value)
        {
            switch (NormalizeGateTemplateName(value))
            {
                case "ALPHA":
                    return "\u03B1";
                case "BETA":
                    return "\u03B2";
                case "GAMMA":
                    return "\u03B3";
                case "DELTA":
                    return "\u03B4";
                default:
                    return (value ?? "").Trim();
            }
        }

        private static GalaxyGateWaveTemplate ReadWaveTemplate(DataRow waveRow)
        {
            return new GalaxyGateWaveTemplate
            {
                Id = GetInt(waveRow, "wave_id", GetInt(waveRow, "WaveID", 0)),
                NpcId = GetInt(waveRow, "npc_id", GetInt(waveRow, "NpcID", 0)),
                NpcCount = GetInt(waveRow, "npc_count", GetInt(waveRow, "Count", 0)),
                Multiplier = GetInt(waveRow, "multiplier", GetInt(waveRow, "Multiplier", 1)),
                KeyNpc = GetInt(waveRow, "key_npc", GetInt(waveRow, "KeyNpc", 0)),
                MinionsId = GetInt(waveRow, "minions_id", GetInt(waveRow, "MinionsID", 0)),
                MinionsCount = GetInt(waveRow, "minions_count", GetInt(waveRow, "MinionsCount", 0)),
                MinionsMultiplier = GetInt(waveRow, "minions_multiplier", GetInt(waveRow, "MinionsMultiplier", 1))
            };
        }

        private static int GetInt(DataRow row, string column, int fallback)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(column) || row[column] == DBNull.Value)
                return fallback;
            return Convert.ToInt32(row[column]);
        }
    }
}
