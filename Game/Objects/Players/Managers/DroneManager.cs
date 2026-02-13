using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ow.Game.Objects;
using Ow.Managers;
using Ow.Managers.MySQLManager;
using Ow.Net.netty.commands;
using Ow.Utils;

namespace Ow.Game.Objects.Players.Managers
{
    class DroneManager : AbstractManager
    {
        public List<Drones> DronesList = new List<Drones>();
        public List<int> Config1Designs = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public List<int> Config2Designs = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public bool Apis = false;
        public bool Zeus = false;
        private bool DroneStateDirty = false;
        private DateTime lastDroneStateSave = DateTime.Now;
        private const int DRONE_AUTOSAVE_SECONDS = 60;

        public DroneManager(Player player) : base(player)
        {
            SetDroneInfo();
            SetDroneDesigns();
        }

        private const int DRONE_CHANGE_COOLDOWN_TIME = 3000;

        public const string DEFAULT_FORMATION = "drone_formation_default";
        public const string TURTLE_FORMATION = "drone_formation_f-01-tu";
        public const string ARROW_FORMATION = "drone_formation_f-02-ar";
        public const string LANCE_FORMATION = "drone_formation_f-03-la";
        public const string STAR_FORMATION = "drone_formation_f-04-st";
        public const string PINCER_FORMATION = "drone_formation_f-05-pi";
        public const string DOUBLE_ARROW_FORMATION = "drone_formation_f-06-da";
        public const string DIAMOND_FORMATION = "drone_formation_f-07-di";
        public const string CHEVRON_FORMATION = "drone_formation_f-08-ch";
        public const string MOTH_FORMATION = "drone_formation_f-09-mo";
        public const string CRAB_FORMATION = "drone_formation_f-10-cr";
        public const string HEART_FORMATION = "drone_formation_f-11-he";
        public const string BARRAGE_FORMATION = "drone_formation_f-12-ba";
        public const string BAT_FORMATION = "drone_formation_f-13-bt";
        public const string DOME_FORMATION = "drone_formation_f-3d-dm";
        public const string DRILL_FORMATION = "drone_formation_f-3d-dr";
        public const string RING_FORMATION = "drone_formation_f-3d-rg";
        public const string VETERAN_FORMATION = "drone_formation_f-3d-vt";
        public const string WHEEL_FORMATION = "drone_formation_f-3d-wl";
        public const string WAVE_FORMATION = "drone_formation_f-3d-wv";
        public const string X_FORMATION = "drone_formation_f-3d-x";

        public void Tick()
        {
            ShieldRegeneration();
            ShieldWeaken();
            TryPersistDroneStateByInterval();
        }

        public void SetDroneDesigns()
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var querySet = mySqlClient.ExecuteQueryRow($"SELECT * FROM player_equipment WHERE userId = {Player.Id}");
                if (querySet == null)
                    return;

                dynamic config1Drones = JsonConvert.DeserializeObject(querySet["config1_drones"].ToString());
                dynamic config2Drones = JsonConvert.DeserializeObject(querySet["config2_drones"].ToString());
                dynamic items = JsonConvert.DeserializeObject(querySet["items"].ToString());

                Apis = items["apis"];
                Zeus = items["zeus"];

                for (var i = 0; i < 10; i++)
                {
                    foreach (var designId in config1Drones[i]["designs"])
                        Config1Designs[i] = (int)designId;

                    foreach (var designId in config2Drones[i]["designs"])
                        Config2Designs[i] = (int)designId;
                }
            }
        }

        public void SetDroneInfo()
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                DronesList = new List<Drones>();
                var querySet = mySqlClient.ExecuteQueryRow($"SELECT * FROM player_equipment WHERE userId = {Player.Id}");
                if (querySet == null)
                    return;

                string dronesJson = null;
                if (querySet.Table.Columns.Contains("drone") && querySet["drone"] != null)
                    dronesJson = querySet["drone"].ToString();
                else if (querySet.Table.Columns.Contains("drones") && querySet["drones"] != null)
                    dronesJson = querySet["drones"].ToString();

                if (string.IsNullOrWhiteSpace(dronesJson))
                    return;

                var token = JToken.Parse(dronesJson);
                if (token.Type == JTokenType.Array)
                {
                    var list = token.ToObject<List<Drones>>();
                    if (list != null)
                        DronesList.AddRange(list);
                }
                else if (token.Type == JTokenType.Object)
                {
                    var drone = token.ToObject<Drones>();
                    if (drone != null)
                        DronesList.Add(drone);
                }

                foreach (var drone in DronesList.Where(x => x != null))
                {
                    if (drone.Damage < 0)
                        drone.Damage = 0;
                    else if (drone.Damage > 100)
                        drone.Damage = 100;

                    drone.Level = getdronelevel(drone.Experience);
                }

                DroneStateDirty = false;
            }
        }

        public void UpdateDrones(bool updateItems = false)
        {
            if (updateItems)
            {
                Config1Designs = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                Config2Designs = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                SetDroneInfo();
                SetDroneDesigns();
            }

            string drones = GetDronesPacket(0);
            Player.SendPacket(drones);
            Player.SendPacketToInRangePlayers(drones);

            var droneFormationChangeCommand = DroneFormationChangeCommand.write(Player.Id, GetSelectedFormationId(Player.Settings.InGameSettings.selectedFormation));
            Player.SendCommand(droneFormationChangeCommand);
            Player.SendCommandToInRangePlayers(droneFormationChangeCommand);
        }

        public string GetDronesPacket(int exp)
        {
            var designs = Player.CurrentConfig == 1 ? Config1Designs : Config2Designs;
            var packetParts = new List<string>();

            var drones = DronesList
                .Where(x => x != null)
                .OrderBy(x => x.Id)
                .ToList();

            if (drones.Count == 0)
            {
                var fallbackLevel = getdronelevel(exp);

                for (var i = 0; i < 8; i++)
                    packetParts.Add($"2|{fallbackLevel}|{GetDesignId(designs[i])}");

                if (Apis)
                    packetParts.Add($"3|{fallbackLevel}|{GetDesignId(designs[8])}");

                if (Zeus)
                    packetParts.Add($"4|{fallbackLevel}|{GetDesignId(designs[9])}");
            }
            else
            {
                for (var i = 0; i < drones.Count; i++)
                {
                    var drone = drones[i];
                    var level = getdronelevel(drone.Experience);

                    if (level < 1)
                        level = 1;
                    else if (level > 6)
                        level = 6;

                    var designId = i < designs.Count ? GetDesignId(designs[i]) : 0;
                    packetParts.Add($"{drone.DroneType}|{level}|{designId}");
                }
            }

            var dronePacket = string.Join("|", packetParts);
            return "0|n|d|" + Player.Id + "|" + dronePacket;
        }

        public int GetDesignId(int designItemId)
        {
            if (designItemId >= 120 && designItemId < 130)
                return 1;
            else if (designItemId >= 130 && designItemId < 140)
                return 2;
            return 0;
        }

        public DateTime regenerationCooldown = new DateTime();
        public void ShieldRegeneration()
        {
            if (regenerationCooldown.AddSeconds(1) >= DateTime.Now || Player.Settings.InGameSettings.selectedFormation != DIAMOND_FORMATION || Player.CurrentShieldPoints >= Player.MaxShieldPoints) return;

            int regeneration = Maths.GetPercentage(Player.MaxShieldPoints, (Player.Settings.InGameSettings.selectedFormation == DIAMOND_FORMATION ? 1 : 0));

            Player.CurrentShieldPoints += (regeneration > 5000 ? 5000 : regeneration);
            Player.UpdateStatus();

            regenerationCooldown = DateTime.Now;
        }

        public DateTime shieldWeakenCooldown = new DateTime();
        public void ShieldWeaken()
        {
            if (shieldWeakenCooldown.AddSeconds(1) >= DateTime.Now || (Player.Settings.InGameSettings.selectedFormation != MOTH_FORMATION && Player.Settings.InGameSettings.selectedFormation != WHEEL_FORMATION) || Player.CurrentShieldPoints <= 0) return;

            int amount = Maths.GetPercentage(Player.MaxShieldPoints, (Player.Settings.InGameSettings.selectedFormation == MOTH_FORMATION || Player.Settings.InGameSettings.selectedFormation == WHEEL_FORMATION ? 1 : 0));

            Player.CurrentShieldPoints -= amount;
            Player.UpdateStatus();

            shieldWeakenCooldown = DateTime.Now;
        }

        public DateTime formationCooldown = new DateTime();
        public void ChangeDroneFormation(string NewFormationID)
        {
            if (NewFormationID == Player.Settings.InGameSettings.selectedFormation) return;

            if (formationCooldown.AddMilliseconds(TimeManager.FORMATION_COOLDOWN) < DateTime.Now || Player.Storage.GodMode)
            {
                Player.SendCooldown(DEFAULT_FORMATION, DRONE_CHANGE_COOLDOWN_TIME);

                string oldSelectedItem = Player.Settings.InGameSettings.selectedFormation;
                Player.Settings.InGameSettings.selectedFormation = NewFormationID;
                Player.SettingsManager.SendNewItemStatus(oldSelectedItem);
                Player.SettingsManager.SendNewItemStatus(NewFormationID);
                Player.Settings.InGameSettings.selectedFormation = NewFormationID;

                var formationCommand = DroneFormationChangeCommand.write(Player.Id, GetSelectedFormationId(NewFormationID));
                Player.SendCommand(formationCommand);
                Player.SendCommandToInRangePlayers(formationCommand);

                Player.UpdateStatus();
                Player.SettingsManager.SendNewItemStatus(NewFormationID);

                formationCooldown = DateTime.Now;
            }
        }

        public static int getdronelevel(int xp)
        {
            // So level 1 needs 0 XP, level 2 needs 100 XP, level 3 needs 200 XP, level 4 needs 400 XP, level 5 needs 800 XP, level 6 needs 1600 XP
            if (xp < 100)
                return 1;
            if (xp < 300)
                return 2;
            if (xp < 700)
                return 3;
            if (xp < 1500)
                return 4;
            if (xp < 3100)
                return 5;
            return 6;
        }

        public void GetActiveDroneLevelBonus(out int damagePercent, out int shieldPercent)
        {
            damagePercent = 0;
            shieldPercent = 0;

            foreach (var drone in GetActiveDrones())
            {
                var level = getdronelevel(drone.Experience);
                if (level < 1)
                    level = 1;
                else if (level > 6)
                    level = 6;

                damagePercent += (level - 1) * 2;
                shieldPercent += (level - 1) * 4;
            }
        }

        public void ApplyDeathDamage()
        {
            if (DronesList == null || DronesList.Count == 0)
                return;

            var changed = false;
            var destroyed = new List<Drones>();

            foreach (var drone in GetActiveDrones())
            {
                var droneDamage = drone.DroneType == 1 ? 4 : 2; // Flax takes more damage than Iris/Apis/Zeus.
                var nextDamage = drone.Damage + droneDamage;
                if (nextDamage > 100)
                    nextDamage = 100;

                if (nextDamage != drone.Damage)
                {
                    drone.Damage = nextDamage;
                    changed = true;
                }

                if (drone.Damage >= 100)
                    destroyed.Add(drone);
            }

            if (destroyed.Count > 0)
            {
                foreach (var drone in destroyed)
                    DronesList.Remove(drone);

                changed = true;
            }

            if (!changed)
                return;

            UpdateDrones();
            QueryManager.SetEquipment(Player);
            Player.UpdateStatus();
            MarkDroneStateDirty();
            PersistDroneStateNow();
        }

        public void AddNpcKillExperience(int xp = 2)
        {
            if (xp <= 0 || DronesList == null || DronesList.Count == 0)
                return;

            var levelChanged = false;
            foreach (var drone in GetActiveDrones())
            {
                var previousLevel = getdronelevel(drone.Experience);
                if (previousLevel >= 6)
                    continue;

                drone.Experience += xp;
                MarkDroneStateDirty();

                var newLevel = getdronelevel(drone.Experience);
                if (newLevel < 1)
                    newLevel = 1;
                else if (newLevel > 6)
                    newLevel = 6;

                if (newLevel != previousLevel)
                {
                    drone.Level = newLevel;
                    levelChanged = true;
                    MarkDroneStateDirty();
                }
            }

            if (!levelChanged)
                return;

            // Sends drone update packets to player + in-range players.
            UpdateDrones();
            QueryManager.SetEquipment(Player);
            Player.UpdateStatus();
            PersistDroneStateNow();
        }

        private List<Drones> GetActiveDrones()
        {
            return DronesList
                .Where(x => x != null && x.Damage < 100)
                .ToList();
        }

        private void MarkDroneStateDirty()
        {
            DroneStateDirty = true;
        }

        private void TryPersistDroneStateByInterval()
        {
            if (!DroneStateDirty)
                return;

            if (lastDroneStateSave.AddSeconds(DRONE_AUTOSAVE_SECONDS) > DateTime.Now)
                return;

            PersistDroneStateNow();
        }

        private void PersistDroneStateNow()
        {
            if (!DroneStateDirty)
                return;

            if (QueryManager.SavePlayer.Drones(Player))
            {
                DroneStateDirty = false;
                lastDroneStateSave = DateTime.Now;
            }
        }

        public static int GetSelectedFormationId(string formation)
        {
            switch (formation)
            {
                case DEFAULT_FORMATION:
                    return 0;
                case TURTLE_FORMATION:
                    return 1;
                case ARROW_FORMATION:
                    return 2;
                case LANCE_FORMATION:
                    return 3;
                case STAR_FORMATION:
                    return 4;
                case PINCER_FORMATION:
                    return 5;
                case DOUBLE_ARROW_FORMATION:
                    return 6;
                case DIAMOND_FORMATION:
                    return 7;
                case CHEVRON_FORMATION:
                    return 8;
                case MOTH_FORMATION:
                    return 9;
                case CRAB_FORMATION:
                    return 10;
                case HEART_FORMATION:
                    return 11;
                case BARRAGE_FORMATION:
                    return 12;
                case BAT_FORMATION:
                    return 13;
                case RING_FORMATION:
                    return 14;
                case DRILL_FORMATION:
                    return 15;
                case VETERAN_FORMATION:
                    return 16;
                case DOME_FORMATION:
                    return 17;
                case WHEEL_FORMATION:
                    return 18;
                case X_FORMATION:
                    return 19;
                case WAVE_FORMATION:
                    return 20;
                default:
                    return 0;
            }
        }
    }
}
