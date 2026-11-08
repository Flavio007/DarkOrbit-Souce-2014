using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Ow.Utils;

namespace Ow.Game.Ticks
{
    class TickManager
    {
        public static short TICKS_PER_SECOND = 84;
        public static long CurrentTickId { get; private set; }

        public List<Tick> Ticks = new List<Tick>();

        public void AddTick(Tick tick)
        {
            lock (Ticks) if (tick != null && !Ticks.Contains(tick)) Ticks.Add(tick);
        }

        public void RemoveTick(Tick tick)
        {
            lock (Ticks) Ticks.Remove(tick);
        }

        public bool Exists(Tick tickable)
        {
            lock (Ticks) return Ticks.Contains(tickable);
        }

        private readonly Dictionary<System.Type, DateTime> lastFailure = new Dictionary<System.Type, DateTime>();
        public void TickOnce()
        {
            CurrentTickId++;
            Tick[] current;
            lock (Ticks) current = Ticks.ToArray();
            foreach (var tick in current)
            {
                if (tick == null) continue;
                var started = Stopwatch.GetTimestamp();
                try { tick.Tick(); }
                catch (Exception e)
                {
                    DateTime previous;
                    if (!lastFailure.TryGetValue(tick.GetType(), out previous) || (DateTime.UtcNow - previous).TotalSeconds >= 10)
                    {
                        lastFailure[tick.GetType()] = DateTime.UtcNow;
                        Logger.Log("error_log", "[TickManager] " + tick.GetType().FullName + " failed; remaining ticks will continue: " + e);
                    }
                }
                var elapsed = (Stopwatch.GetTimestamp() - started) * 1000 / Stopwatch.Frequency;
                if (elapsed >= 1000)
                    Logger.Log("error_log", "[TickManager] Slow tick " + tick.GetType().FullName + ": " + elapsed + "ms");
            }
        }

        public async void Tick()
        {
            try
            {
                while (true)
                {
                    TickOnce();

                    await Task.Delay(TICKS_PER_SECOND);
                }
            }
            catch (Exception e)
            {
                Out.WriteLine("Tick void exception: " + e, "TickManager.cs");
                Logger.Log("error_log", $"- [TickManager.cs] Tick void exception: {e}");
            }
        }
    }
}
