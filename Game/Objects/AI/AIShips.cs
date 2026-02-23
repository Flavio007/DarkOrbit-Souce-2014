using Ow.Game.Movements;
using Ow.Utils;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Game.Objects.AI
{
    class AIShips
    {
        private static readonly ConcurrentDictionary<int, FakePlayer> FakePlayers = new ConcurrentDictionary<int, FakePlayer>();

        public static FakePlayer CreateStationaryFakePlayer(Ship ship, Spacemap map, Position nearPosition, int factionId)
        {
            var id = CreateUniqueId(map);
            var name = "FAKE-" + id;
            var spawn = BuildSpawnPosition(map, nearPosition);

            var fakePlayer = new FakePlayer(id, name, ship, map, spawn, factionId);
            FakePlayers[id] = fakePlayer;
            return fakePlayer;
        }

        public static IReadOnlyCollection<FakePlayer> GetAll()
        {
            return FakePlayers.Values.ToList();
        }

        public static bool Remove(int id)
        {
            FakePlayer fakePlayer;
            if (!FakePlayers.TryRemove(id, out fakePlayer))
                return false;

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
    }
}
