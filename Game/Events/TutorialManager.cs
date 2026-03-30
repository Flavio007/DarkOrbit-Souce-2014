using Ow.Game.Movements;
using Ow.Game.Objects;
using Ow.Game.Objects.AI;
using Ow.Game.Objects.Players;
using Ow.Game.Objects.Players.Managers;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ow.Game.Events
{
    class TutorialManager
    {
        private sealed class TutorialInstance
        {
            public int OwnerId { get; set; }
            public int OwnerFactionId { get; set; }
            public int MapId { get; set; }
            public Spacemap Spacemap { get; set; }
            public FakePlayer Phoenix { get; set; }
            public List<Npc> Streuners { get; } = new List<Npc>();
            public FakePlayer SupportVengeance { get; set; }
            public FakePlayer SupportGoliath { get; set; }
            public TutorialExitPortal ExitPortal { get; set; }
            public bool RewardsSpawned { get; set; }
        }

        private sealed class TutorialExitPortal : Portal
        {
            private readonly TutorialManager manager;
            private readonly int ownerId;

            public TutorialExitPortal(TutorialManager manager, int ownerId, Spacemap spacemap, Position position, Position targetPosition, int targetSpacemapId, int graphicsId, int factionId)
                : base(spacemap, position, targetPosition, targetSpacemapId, graphicsId, factionId, true, true, false)
            {
                this.manager = manager;
                this.ownerId = ownerId;
            }

            public override bool IsVisibleTo(Player player)
            {
                return player != null && player.Id == ownerId;
            }

            public override bool CanInteract(Player player)
            {
                return player != null && player.Id == ownerId;
            }

            public override async void Click(GameSession gameSession)
            {
                try
                {
                    var player = gameSession?.Player;
                    if (player == null || player.Id != ownerId)
                        return;

                    if (!Working || GameManager.GetSpacemap(TargetSpaceMapId) == null || TargetPosition == null)
                        return;

                    if (player.Storage.Jumping)
                        return;

                    player.Storage.Jumping = true;
                    player.SendCommand(ActivatePortalCommand.write(TargetSpaceMapId, Id));
                    await Task.Delay(JUMP_DELAY);

                    player.LastCombatTime = DateTime.Now.AddSeconds(-999);
                    player.Spacemap.RemoveCharacter(player);
                    player.CurrentInRangePortalId = -1;
                    player.Deselection();
                    player.Storage.InRangeAssets.Clear();
                    player.InRangeCharacters.Clear();
                    player.SetPosition(TargetPosition);
                    player.Spacemap = GameManager.GetSpacemap(TargetSpaceMapId);
                    player.Spacemap.AddAndInitPlayer(player);
                    player.Storage.Jumping = false;

                    manager.CompleteTutorial(ownerId);
                }
                catch (Exception e)
                {
                    Logger.Log("error_log", $"- [TutorialManager.cs] TutorialExitPortal.Click exception: {e}");
                    if (gameSession?.Player != null)
                        gameSession.Player.Storage.Jumping = false;
                }
            }
        }

        public const int TutorialMapId = 255;
        private const int TutorialPortalGraphicId = 55;
        private const int TutorialStreunerShipId = 84;
        private const int TutorialMapStartId = 20000;
        private const int SupportShipApproachOffsetX = 320;
        private const int SupportShipApproachOffsetY = 120;
        private const int ExitPortalOffsetY = 520;

        private static readonly Position TutorialCenter = new Position(5225, 3250);
        private static readonly Position[] StreunerPositions =
        {
            new Position(4680, 2870),
            new Position(5770, 2870),
            new Position(5225, 3980)
        };

        private static readonly Position SupportVengeancePosition = new Position(4520, 2290);
        private static readonly Position SupportGoliathPosition = new Position(5930, 2290);
        private static readonly Position ExitPortalPosition = new Position(5225, 1880);

        private readonly object tutorialLock = new object();
        private readonly Dictionary<int, TutorialInstance> instancesByOwner = new Dictionary<int, TutorialInstance>();
        private readonly Dictionary<int, TutorialInstance> instancesByMap = new Dictionary<int, TutorialInstance>();
        private int nextDynamicMapId = TutorialMapStartId;

        public void PreparePlayerForLogin(Player player)
        {
            if (player == null || !ShouldUseTutorial(player))
                return;

            TutorialInstance instance;
            lock (tutorialLock)
            {
                instance = GetOrCreateInstance(player);
                EnsureTutorialScene(instance, player);
            }

            player.Spacemap = instance.Spacemap;
            player.SetPosition(ResolvePlayerPosition(player, instance.Spacemap));
            player.LastPosition.map = TutorialMapId;
            player.LastPosition.x = player.Position.X;
            player.LastPosition.y = player.Position.Y;
        }

        public void HandleNpcDestroyed(Npc npc, Player destroyer)
        {
            if (npc == null || destroyer == null || npc.Spacemap == null)
                return;

            lock (tutorialLock)
            {
                if (!instancesByMap.TryGetValue(npc.Spacemap.Id, out var instance))
                    return;

                if (instance.OwnerId != destroyer.Id)
                    return;

                var removed = instance.Streuners.RemoveAll(x => x != null && x.Id == npc.Id);
                if (removed <= 0 || instance.Streuners.Count > 0 || instance.RewardsSpawned)
                    return;

                SpawnRewards(instance, destroyer);
            }
        }

        public void CompleteTutorial(int ownerId)
        {
            lock (tutorialLock)
            {
                if (!instancesByOwner.TryGetValue(ownerId, out var instance))
                    return;

                instancesByOwner.Remove(ownerId);
                instancesByMap.Remove(instance.MapId);
                CleanupInstance(instance);
            }
        }

        private void CleanupInstance(TutorialInstance instance)
        {
            if (instance == null)
                return;

            instance.ExitPortal?.Remove();

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
        }

        private bool ShouldUseTutorial(Player player)
        {
            if (player.LastPosition != null && player.LastPosition.map == TutorialMapId)
                return true;

            return player.Spacemap != null && player.Spacemap.VisualMapId == TutorialMapId;
        }

        private TutorialInstance GetOrCreateInstance(Player player)
        {
            if (instancesByOwner.TryGetValue(player.Id, out var instance) && instance.Spacemap != null)
            {
                instance.OwnerFactionId = player.FactionId;
                return instance;
            }

            var options = new OptionsBase
            {
                StarterMap = true,
                PvpMap = false,
                RangeDisabled = false,
                CloakBlocked = true,
                LogoutBlocked = false,
                DeathLocationRepair = false
            };

            var mapId = AllocateDynamicMapId();
            var map = new Spacemap(
                mapId,
                $"Tutorial-{player.Id}",
                0,
                null,
                null,
                null,
                options,
                null,
                TutorialMapId,
                true);

            map.Instance = true;
            GameManager.Spacemaps.TryAdd(map.Id, map);

            instance = new TutorialInstance
            {
                OwnerId = player.Id,
                OwnerFactionId = player.FactionId,
                MapId = map.Id,
                Spacemap = map
            };

            instancesByOwner[player.Id] = instance;
            instancesByMap[map.Id] = instance;
            return instance;
        }

        private int AllocateDynamicMapId()
        {
            while (GameManager.GetSpacemap(nextDynamicMapId) != null)
                nextDynamicMapId++;

            return nextDynamicMapId++;
        }

        private void EnsureTutorialScene(TutorialInstance instance, Player owner)
        {
            if (instance.RewardsSpawned)
                return;

            if (instance.Phoenix != null && !instance.Phoenix.Destroyed && instance.Streuners.Count > 0)
                return;

            instance.Phoenix = CreatePhoenix(instance, owner);
            if (instance.Phoenix == null)
                return;

            instance.Streuners.Clear();

            var streunerShip = GameManager.GetShip(TutorialStreunerShipId);
            if (streunerShip == null)
                return;

            foreach (var position in StreunerPositions)
            {
                var streuner = new Npc(Randoms.CreateRandomID(), streunerShip, instance.Spacemap, new Position(position.X, position.Y), 0);
                streuner.Respawnable = false;
                streuner.Aggressive = true;
                streuner.AgroRange = streuner.RenderRange;
                streuner.ReceiveAttack(instance.Phoenix);
                streuner.NpcAI.AIOption = NpcAIOption.FLY_TO_ENEMY;
                instance.Streuners.Add(streuner);
            }

            instance.RewardsSpawned = false;
            instance.SupportVengeance = null;
            instance.SupportGoliath = null;
            instance.ExitPortal?.Remove();
            instance.ExitPortal = null;
        }

        private FakePlayer CreatePhoenix(TutorialInstance instance, Player owner)
        {
            var ship = GameManager.GetShip(Ship.PHOENIX);
            if (ship == null)
                return null;

            var fakePlayer = new FakePlayer(
                Randoms.CreateRandomID(),
                $"tutorial_phoenix_{owner.Id}",
                ship,
                instance.Spacemap,
                new Position(TutorialCenter.X, TutorialCenter.Y),
                owner.FactionId);

            fakePlayer.ApplyProfile("Tutorial Phoenix", GameManager.GetClan(0), 1, 1, "", null, 0, 0, 0, new List<Drones>());
            fakePlayer.ConfigureLoadout(ship.BaseHitpoints, 0, 0, ship.BaseSpeed, 0, false, 0);
            fakePlayer.SetMinimumHitpoints(1);
            fakePlayer.CurrentShieldPoints = 0;
            fakePlayer.UpdateStatus();
            return fakePlayer;
        }

        private void SpawnRewards(TutorialInstance instance, Player owner)
        {
            instance.SupportVengeance = CreateSupportShip(instance, owner, Ship.VENGEANCE, "Tutorial Vengeance", SupportVengeancePosition, CreateDrones(1, 8, 6));
            instance.SupportGoliath = CreateSupportShip(instance, owner, Ship.GOLIATH, "Tutorial Goliath", SupportGoliathPosition, CreateDrones(2, 8, 6));

            MoveSupportShipTowardsPhoenix(instance, instance.SupportVengeance, true);
            MoveSupportShipTowardsPhoenix(instance, instance.SupportGoliath, false);

            var exitPortalPosition = ResolveExitPortalPosition(instance);

            instance.ExitPortal = new TutorialExitPortal(
                this,
                owner.Id,
                instance.Spacemap,
                exitPortalPosition,
                owner.GetBasePosition(),
                owner.GetBaseMapId(),
                TutorialPortalGraphicId,
                owner.FactionId);

            GameManager.SendCommandToMap(instance.Spacemap.Id, instance.ExitPortal.GetAssetCreateCommand());

            instance.RewardsSpawned = true;
        }

        private FakePlayer CreateSupportShip(TutorialInstance instance, Player owner, int shipId, string name, Position position, List<Drones> drones)
        {
            var ship = GameManager.GetShip(shipId);
            if (ship == null)
                return null;

            var fakePlayer = new FakePlayer(
                Randoms.CreateRandomID(),
                name,
                ship,
                instance.Spacemap,
                new Position(position.X, position.Y),
                owner.FactionId);

            fakePlayer.ApplyProfile(name, GameManager.GetClan(0), 1, 1, "", null, 0, 0, 0, drones);
            fakePlayer.SkillTree.shieldEngineering = 5;
            fakePlayer.shieldeng = true;
            fakePlayer.CurrentShieldPoints = fakePlayer.MaxShieldPoints;
            fakePlayer.UpdateStatus();
            return fakePlayer;
        }

        private void MoveSupportShipTowardsPhoenix(TutorialInstance instance, FakePlayer supportShip, bool approachFromLeft)
        {
            if (instance?.Phoenix == null || supportShip == null)
                return;

            var xOffset = approachFromLeft ? -SupportShipApproachOffsetX : SupportShipApproachOffsetX;
            var targetPosition = ClampToMap(
                instance.Spacemap,
                new Position(
                    instance.Phoenix.Position.X + xOffset,
                    instance.Phoenix.Position.Y - SupportShipApproachOffsetY));

            Movement.Move(supportShip, targetPosition);
        }

        private Position ResolveExitPortalPosition(TutorialInstance instance)
        {
            if (instance?.Phoenix == null)
                return new Position(ExitPortalPosition.X, ExitPortalPosition.Y);

            return ClampToMap(
                instance.Spacemap,
                new Position(
                    instance.Phoenix.Position.X,
                    instance.Phoenix.Position.Y + ExitPortalOffsetY));
        }

        private static List<Drones> CreateDrones(int droneType, int amount, int level)
        {
            var drones = new List<Drones>();
            for (var i = 0; i < amount; i++)
                drones.Add(new Drones(i + 1, (byte)droneType, 0, 0, level));

            return drones;
        }

        private Position ResolvePlayerPosition(Player player, Spacemap map)
        {
            if (player.Position != null && player.Spacemap != null && player.Spacemap.VisualMapId == TutorialMapId)
                return ClampToMap(map, player.Position);

            if (player.LastPosition != null && player.LastPosition.map == TutorialMapId)
                return ClampToMap(map, new Position(player.LastPosition.x, player.LastPosition.y));

            return new Position(TutorialCenter.X, TutorialCenter.Y);
        }

        private static Position ClampToMap(Spacemap map, Position position)
        {
            if (map == null || position == null)
                return new Position(TutorialCenter.X, TutorialCenter.Y);

            var x = position.X;
            var y = position.Y;

            if (x < map.Limits[0].X)
                x = map.Limits[0].X;
            else if (x > map.Limits[1].X)
                x = map.Limits[1].X;

            if (y < map.Limits[0].Y)
                y = map.Limits[0].Y;
            else if (y > map.Limits[1].Y)
                y = map.Limits[1].Y;

            return new Position(x, y);
        }
    }
}