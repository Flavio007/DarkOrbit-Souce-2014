using Newtonsoft.Json;
using Ow.Game.Movements;
using Ow.Managers;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Ow.Game.Objects
{
    // Loaded once per process. All coordinates/radii are world units.
    internal sealed class CarrierSettings
    {
        public int WaveCount = 6, MaxMinions = 18, SpawnIntervalMs = 500;
        public int WaveCooldownMs = 25000, AuraCooldownMs = 18000;
        public int AuraDurationMs = 5000, AuraRadius = 700, AuraDamagePerSecond = 6000;
        public int SpawnOffsetX = 0, SpawnOffsetY = -240;
        public int LaserCooldownMs = 12000, LaserDamage = 24000, LaserFadeMs = 1500;
        public int HpLossPerWave = 200000, ReplacementCooldownMs = 3000;
        public int HealDelayMs = 60000, HealPercentPerSecond = 2;
        public int MinionReturnMs = 30000, MinionOrbitRadius = 400, MinionLeashRadius = 1800;
        public static readonly CarrierSettings Current = Load();
        private static CarrierSettings Load()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "devourer-carrier.json");
            var value = File.Exists(path) ? JsonConvert.DeserializeObject<CarrierSettings>(File.ReadAllText(path)) : new CarrierSettings();
            if (value == null || value.WaveCount < 1 || value.WaveCount > 50 || value.MaxMinions < 1 || value.MaxMinions > 100 ||
                value.SpawnIntervalMs < 100 || value.SpawnIntervalMs > 10000 || value.WaveCooldownMs < 1000 ||
                value.AuraCooldownMs < 1000 || value.AuraDurationMs < 100 || value.AuraDurationMs > 60000 ||
                value.AuraRadius < 1 || value.AuraRadius > 5000 || value.AuraDamagePerSecond < 0 || value.AuraDamagePerSecond > 1000000 ||
                value.WaveCooldownMs > 3600000 || value.AuraCooldownMs > 3600000 ||
                Math.Abs((long)value.SpawnOffsetX) > 5000 || Math.Abs((long)value.SpawnOffsetY) > 5000 ||
                value.LaserCooldownMs < 1000 || value.LaserCooldownMs > 3600000 || value.LaserDamage < 1 || value.LaserDamage > 1000000 ||
                value.LaserFadeMs < 100 || value.LaserFadeMs > 10000 || value.HpLossPerWave < 1 || value.ReplacementCooldownMs < 100 ||
                value.HealDelayMs < 1000 || value.HealPercentPerSecond < 1 || value.HealPercentPerSecond > 100 ||
                value.MinionReturnMs < 1000 || value.MinionOrbitRadius < 50 || value.MinionLeashRadius <= value.MinionOrbitRadius)
                throw new InvalidDataException("Invalid devourer-carrier.json");
            return value;
        }
    }

    // Seconds from a monotonic clock: no tick-count, UTC epoch or Int32 uptime arithmetic.
    internal sealed class CarrierTimeline
    {
        public int Mode { get; private set; } // 0 idle, 1 launch, 2 area attack, 3 laser, 4 exit
        public double Age { get; private set; }
        public double Hold { get; private set; }
        public double LaserFade = 1.5;
        public double Opening => Mode == 1 || Mode == 4 ? 16.0 / 15 : Mode == 3 ? LaserFade : 10.0 / 15;
        public double Duration => Mode == 4 ? Opening : 2 * Opening + Hold;
        public void Begin(int mode, double hold) { Mode = mode; Hold = hold; Age = 0; }
        public void Advance(double seconds) { if (Mode == 0) return; Age += Math.Max(0, seconds); if (Age >= Duration) Mode = 0; }
        public bool Active => Mode != 0 && Age >= Opening && Age < Opening + Hold;
    }

    internal sealed class DevourerCarrier
    {
        public const int ShipId = 1105;
        private readonly Npc npc;
        private readonly object sync = new object();
        private readonly CarrierSettings settings = CarrierSettings.Current;
        private readonly CarrierTimeline timeline = new CarrierTimeline();
        private readonly List<Npc> minions = new List<Npc>();
        private double last = Clock(), nextWave, nextAura = 8, nextLaser = 4, nextSpawn, nextDamage, lastCombat, nextHeal;
        private double time;
        private bool combat;
        private bool laserFired;
        private bool replacementPending;
        private int remaining, desiredMinions, previousHp, damageForWave, actionCursor, escortSequence;
        private DateTime observedCombat;
        private static double Clock() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        public DevourerCarrier(Npc owner) {
            npc = owner; previousHp = owner.CurrentHitPoints;
            timeline.LaserFade = settings.LaserFadeMs / 1000.0;
        }

        public string Snapshot()
        {
            lock (sync) return SnapshotCore();
        }
        private string SnapshotCore()
        {
            return "0|n|carrier|" + npc.Id + "|" + timeline.Mode + "|" + (timeline.Mode == 0 ? 0 : (int)(timeline.Age * 1000)) + "|" +
                (timeline.Mode == 0 ? 0 : (int)(timeline.Hold * 1000)) + "|" + settings.AuraRadius + "|" + (combat ? 1 : 0) + "|" + settings.LaserFadeMs;
        }
        private void Publish() => npc.SendPacketToInRangePlayers(Snapshot());
        public void Tick()
        {
            lock (sync) if (!npc.Destroyed) TickCore();
        }
        private void TickCore()
        {
            var now = Clock(); var delta = Math.Max(0, now - last); last = now; time += delta;
            var oldMode = timeline.Mode;
            timeline.Advance(delta);
            bool attacking = npc.Attacking && npc.SelectedCharacter != null && !npc.SelectedCharacter.Destroyed && npc.SelectedCharacter.Spacemap == npc.Spacemap;
            if (combat != attacking || oldMode != timeline.Mode) { combat = attacking; Publish(); }
            if (attacking || npc.LastCombatTime > observedCombat) { lastCombat = time; observedCombat = npc.LastCombatTime; }
            var loss = Math.Max(0, previousHp - npc.CurrentHitPoints);
            damageForWave = (int)Math.Min(int.MaxValue, (long)damageForWave + loss);
            previousHp = npc.CurrentHitPoints;
            if (damageForWave >= settings.HpLossPerWave) {
                var waves = damageForWave / settings.HpLossPerWave;
                damageForWave %= settings.HpLossPerWave;
                desiredMinions = (int)Math.Min(settings.MaxMinions, (long)desiredMinions + (long)waves * settings.WaveCount);
                replacementPending = true;
                nextWave = Math.Min(nextWave, time);
            }
            var removed = minions.RemoveAll(n => {
                if (!n.Destroyed && n.Spacemap == npc.Spacemap && npc.Spacemap.ContainsCharacter(n)) return false;
                Program.TickManager.RemoveTick(n); return true;
            });
            if (removed > 0) {
                replacementPending = true;
                nextWave = Math.Min(nextWave, time + settings.ReplacementCooldownMs / 1000.0);
            }
            if (!combat && time - lastCombat >= settings.HealDelayMs / 1000.0 && time >= nextHeal) {
                nextHeal = time + 1;
                npc.CurrentHitPoints = (int)Math.Min(npc.MaxHitPoints, (long)npc.CurrentHitPoints + Math.Max(1, (long)npc.MaxHitPoints * settings.HealPercentPerSecond / 100));
                previousHp = npc.CurrentHitPoints; npc.UpdateStatus();
            }
            if (time >= 16.0 / 15 && timeline.Mode == 0 && (combat || replacementPending))
            {
                for (int attempt = 0; attempt < 3; attempt++) {
                    int action = (actionCursor + attempt) % 3;
                    if (action == 0 && time >= nextWave && minions.Count < Math.Min(desiredMinions, settings.MaxMinions)) {
                        remaining = Math.Min(settings.WaveCount, Math.Min(desiredMinions, settings.MaxMinions) - minions.Count);
                        timeline.Begin(1, remaining * settings.SpawnIntervalMs / 1000.0 + .5);
                        nextSpawn = 0; nextWave = time + timeline.Duration + (replacementPending ? settings.ReplacementCooldownMs : settings.WaveCooldownMs) / 1000.0;
                    } else if (action == 1 && combat && time >= nextAura) {
                        timeline.Begin(2, settings.AuraDurationMs / 1000.0);
                        nextDamage = 0; nextAura = time + timeline.Duration + settings.AuraCooldownMs / 1000.0;
                    } else if (action == 2 && combat && time >= nextLaser) {
                        timeline.Begin(3, .35); laserFired = false;
                        nextLaser = time + timeline.Duration + settings.LaserCooldownMs / 1000.0;
                    } else continue;
                    actionCursor = (action + 1) % 3; Publish(); break;
                }
            }
            if (timeline.Active && timeline.Mode == 1 && remaining > 0 && timeline.Age >= nextSpawn)
            {
                var ship = GameManager.GetShip(100);
                if (ship != null)
                {
                    var origin = new Position(npc.Position.X + settings.SpawnOffsetX, npc.Position.Y + settings.SpawnOffsetY);
                    var child = new Npc(Randoms.CreateRandomID(), ship, npc.Spacemap, origin, npc.Id) {
                        Respawnable = false, Aggressive = false, CarrierMother = npc, CarrierOrderTime = DateTime.Now,
                        CarrierEscortSlot = escortSequence++ };
                    minions.Add(child);
                    if (combat) child.ReceiveAttack(npc.SelectedCharacter);
                    var index = settings.WaveCount - remaining;
                    Movement.Move(child, new Position(origin.X + (index % 2 == 0 ? -1 : 1) * (120 + index * 35), origin.Y - 200));
                }
                remaining--;
                if (minions.Count >= desiredMinions) replacementPending = false;
                // Never emit a delayed wave in one burst after a stall.
                nextSpawn = timeline.Age + settings.SpawnIntervalMs / 1000.0;
            }
            if (timeline.Mode == 3 && timeline.Active && !laserFired) {
                laserFired = true;
                var target = npc.SelectedCharacter;
                if (target != null && !target.Destroyed && target.Spacemap == npc.Spacemap)
                    npc.FireCarrierLaser(target, settings.LaserDamage);
            }
            if (timeline.Active && timeline.Mode == 2 && timeline.Age >= nextDamage)
            {
                nextDamage = timeline.Age + 1;
                foreach (var player in npc.Spacemap.Characters.Values.OfType<Player>().ToArray())
                    if (!player.Destroyed && player.Attackable() && !player.Storage.IsInSafeZone &&
                        player.Position.DistanceTo(npc.Position) <= settings.AuraRadius)
                        npc.ApplyCarrierDamage(player, settings.AuraDamagePerSecond);
            }
        }
        public void Clear()
        {
            lock (sync) ClearCore();
        }
        private void ClearCore()
        {
            foreach (var child in minions)
            {
                if (!child.Destroyed) { child.Deselection(); child.Destroyed = true; child.Spacemap.RemoveCharacter(child); }
                Program.TickManager.RemoveTick(child);
            }
            minions.Clear();
        }
    }
}
