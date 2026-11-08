using System;
using System.Collections.Generic;

namespace Ow.Game.Objects
{
    // The visible git leaves the extracted door timeline at frame 45, at half speed.
    internal sealed class ClassicCubikonWave
    {
        internal const double DoorFps = 22.5;
        internal const double ExitDelaySeconds = 45.0 / DoorFps;
        internal const double SpacingSeconds = 0.45;
        private static readonly int[] DoorOrder = { 5, 9, 4, 8, 3, 7, 2, 6, 1, 0 };
        private sealed class Release
        {
            internal int Ship, Door;
            internal DateTime OpensAt, ReleasesAt;
            internal bool Opened;
        }
        private readonly List<Release> pending = new List<Release>();
        private DateTime lastOpen = DateTime.MinValue, lastRelease = DateTime.MinValue;
        internal int PendingCount { get { return pending.Count; } }

        internal void Begin(int ship, int count, DateTime now)
        {
            if (pending.Count != 0 || count <= 0) return;
            for (var i = 0; i < count; ++i)
                pending.Add(new Release { Ship = ship, Door = DoorOrder[i % DoorOrder.Length], OpensAt = now.AddSeconds(i * SpacingSeconds) });
        }

        internal void Tick(DateTime now, Action<int> open, Action<int, int> release)
        {
            for (var i = 0; i < pending.Count; ++i)
            {
                var item = pending[i];
                if (!item.Opened && now >= item.OpensAt && (now - lastOpen).TotalSeconds >= SpacingSeconds)
                {
                    item.Opened = true;
                    item.ReleasesAt = now.AddSeconds(ExitDelaySeconds);
                    lastOpen = now;
                    open(item.Door);
                }
                if (item.Opened && now >= item.ReleasesAt && (now - lastRelease).TotalSeconds >= SpacingSeconds)
                {
                    lastRelease = now;
                    pending.RemoveAt(i--);
                    release(item.Ship, item.Door);
                }
            }
        }

        internal void Cancel() { pending.Clear(); lastOpen = lastRelease = DateTime.MinValue; }
    }
}
