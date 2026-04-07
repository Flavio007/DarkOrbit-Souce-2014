using Ow.Game.Objects.Players.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects.Players
{
    class RocketLauncher
    {
        private const int HST1_MAX_LOAD = 3;
        private const int HST2_MAX_LOAD = 5;
        private readonly List<int> launcherLoads = new List<int>();
        private int? lastSentLauncherType;
        private int? lastSentLauncherId;
        private int? lastSentStatusLoad;

        public Player Player { get; set; }

        public int CurrentLoad = 0;

        public int MaxLoad { get; set; }

        public string LoadLootId { get; set; }

        public bool ReloadingActive = false;

        public bool LoadLaunchersInParallel { get; set; } = true;

        public RocketLauncher(Player player) { Player = player; LoadLootId = AmmunitionManager.ROCKET_LAUNCHER_ECO_10; }

        public void Tick()
        {
            if (ReloadingActive)
                Reload();
        }

        public DateTime CooldownTime = new DateTime();

        public DateTime LastReloadTime = new DateTime();

        public void SetLauncherLoads(IEnumerable<int> loads)
        {
            launcherLoads.Clear();

            if (loads == null)
                return;

            launcherLoads.AddRange(loads.Where(load => load > 0).OrderByDescending(load => load));
        }

        private int GetStatusMaxLoad()
        {
            if (MaxLoad <= 0)
                return 0;

            if (launcherLoads.Count > 0)
                return launcherLoads[0];

            return MaxLoad <= HST1_MAX_LOAD ? HST1_MAX_LOAD : HST2_MAX_LOAD;
        }

        private int GetLauncherTypeForStatus()
        {
            if (MaxLoad <= 0)
                return 0;

            return GetStatusMaxLoad() <= HST1_MAX_LOAD ? 1 : 2;
        }

        public void SendStatus(bool force = false)
        {
            var launcherType = GetLauncherTypeForStatus();
            var launcherId = Player.AttackManager.GetSelectedLauncherId();
            var statusLoad = Math.Min(CurrentLoad, GetStatusMaxLoad());

            if (!force &&
                lastSentLauncherType == launcherType &&
                lastSentLauncherId == launcherId &&
                lastSentStatusLoad == statusLoad)
                return;

            lastSentLauncherType = launcherType;
            lastSentLauncherId = launcherId;
            lastSentStatusLoad = statusLoad;

            Player.SendPacket($"0|RL|S|{launcherType}|{launcherId}|{statusLoad}");
        }

        public List<int> GetVolleyLoads(int loadedRockets)
        {
            var remaining = Math.Max(loadedRockets, 0);
            var volleyLoads = new List<int>();

            foreach (var launcherLoad in launcherLoads)
            {
                if (remaining <= 0)
                    break;

                var volleyLoad = Math.Min(remaining, launcherLoad);
                if (volleyLoad <= 0)
                    continue;

                volleyLoads.Add(volleyLoad);
                remaining -= volleyLoad;
            }

            var fallbackLoad = GetStatusMaxLoad();
            if (fallbackLoad <= 0)
                fallbackLoad = MaxLoad;

            while (remaining > 0 && fallbackLoad > 0)
            {
                var volleyLoad = Math.Min(remaining, fallbackLoad);
                volleyLoads.Add(volleyLoad);
                remaining -= volleyLoad;
            }

            return volleyLoads;
        }

        private List<int> GetCurrentLauncherLoads(int loadedRockets)
        {
            var currentLauncherLoads = new List<int>();

            if (launcherLoads.Count == 0)
            {
                if (loadedRockets > 0)
                    currentLauncherLoads.Add(Math.Min(loadedRockets, Math.Max(MaxLoad, 0)));

                return currentLauncherLoads;
            }

            for (var index = 0; index < launcherLoads.Count; index++)
                currentLauncherLoads.Add(0);

            var remaining = Math.Max(loadedRockets, 0);
            while (remaining > 0)
            {
                var loadedAnyRocket = false;

                for (var index = 0; index < launcherLoads.Count && remaining > 0; index++)
                {
                    if (currentLauncherLoads[index] >= launcherLoads[index])
                        continue;

                    currentLauncherLoads[index]++;
                    remaining--;
                    loadedAnyRocket = true;
                }

                if (!loadedAnyRocket)
                    break;
            }

            return currentLauncherLoads;
        }

        private int GetReloadStep(int maxPossibleLoad)
        {
            if (!LoadLaunchersInParallel || launcherLoads.Count <= 1)
                return 1;

            var currentLauncherLoads = GetCurrentLauncherLoads(CurrentLoad);
            var activeLaunchers = 0;

            for (var index = 0; index < currentLauncherLoads.Count; index++)
            {
                if (currentLauncherLoads[index] < launcherLoads[index])
                    activeLaunchers++;
            }

            if (activeLaunchers <= 0)
                return 1;

            return Math.Min(activeLaunchers, maxPossibleLoad - CurrentLoad);
        }

        public void Reload()
        {
            if (CooldownTime > DateTime.Now) return;
            if (LastReloadTime.AddSeconds(Player.RocketLauncherSpeed) > DateTime.Now) return;
            if (MaxLoad <= 0)
            {
                ReloadingActive = false;
                CurrentLoad = 0;
                Player.SettingsManager.SendNewItemStatus(CpuManager.ROCKET_LAUNCHER);
                SendStatus();
                return;
            }

            var availableAmmo = Player.GetAmmoCount(Player.Settings.InGameSettings.selectedRocketLauncher);
            var maxPossibleLoad = Math.Min(MaxLoad, Math.Max(availableAmmo, 0));
            if (maxPossibleLoad <= 0)
            {
                ReloadingActive = false;
                CurrentLoad = 0;
                Player.SettingsManager.SendNewItemStatus(CpuManager.ROCKET_LAUNCHER);
                SendStatus();
                return;
            }

            if (CurrentLoad >= maxPossibleLoad)
            {
                CurrentLoad = maxPossibleLoad;
                ReloadingActive = false;
                Player.SettingsManager.SendNewItemStatus(CpuManager.ROCKET_LAUNCHER);
                SendStatus();
                return;
            }

            ReloadingActive = true;
            CurrentLoad += GetReloadStep(maxPossibleLoad);
            SendStatus();
            Player.SettingsManager.SendNewItemStatus(CpuManager.ROCKET_LAUNCHER);
            LastReloadTime = DateTime.Now;
        }

        public void ResetLoadAfterFire()
        {
            CurrentLoad = 0;
            ReloadingActive = false;
            LastReloadTime = DateTime.Now;
            Player.SettingsManager.SendNewItemStatus(CpuManager.ROCKET_LAUNCHER);
            SendStatus();
        }

        public void ChangeLoad(string lootId)
        {
            ReloadingActive = false;
            CurrentLoad = 0;
            LoadLootId = lootId;
            LastReloadTime = DateTime.Now;
            Player.SettingsManager.SendNewItemStatus(CpuManager.ROCKET_LAUNCHER);
            SendStatus();
        }
    }
}

