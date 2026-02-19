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

        public Player Player { get; set; }

        public int CurrentLoad = 0;

        public int MaxLoad { get; set; }

        public string LoadLootId { get; set; }

        public bool ReloadingActive = false;

        public RocketLauncher(Player player) { Player = player; LoadLootId = AmmunitionManager.ECO_10; }

        public void Tick()
        {
            if (ReloadingActive)
                Reload();
        }

        public DateTime CooldownTime = new DateTime();

        public DateTime LastReloadTime = new DateTime();

        private int GetLauncherTypeForStatus()
        {
            return MaxLoad <= HST1_MAX_LOAD ? 1 : 2;
        }

        public void SendStatus()
        {
            var launcherType = GetLauncherTypeForStatus();
            Player.SendPacket($"0|RL|S|{launcherType}|{Player.AttackManager.GetSelectedLauncherId()}|{CurrentLoad}");
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
            CurrentLoad++;
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
