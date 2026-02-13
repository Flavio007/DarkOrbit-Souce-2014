using MySQLManager.Database.Session_Details.Interfaces;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Ow.Game;
using Newtonsoft.Json;
using Ow.Game.Objects.Players;
using Ow.Game.Objects.Stations;
using Ow.Game.Objects.Players.Managers;
using Ow.Game.Objects;
using Ow.Game.Objects.Collectables;
using Ow.Managers.MySQLManager;
using Ow.Game.Movements;
using Newtonsoft.Json.Linq;
using Ow.Net;
using Ow.Net.netty.commands;

namespace Ow.Managers
{
    class QueryManager
    {
        public class SavePlayer
        {
            public static void Settings(Player player, string target, object settings)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                    mySqlClient.ExecuteNonQuery($"UPDATE player_settings SET {target} = '{JsonConvert.SerializeObject(settings)}' WHERE userId = {player.Id}");
            }

            public static void Information(Player player)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                    mySqlClient.ExecuteNonQuery($"UPDATE player_accounts SET data = '{JsonConvert.SerializeObject(player.Data)}', nanohull = {player.CurrentNanoHull}, destructions = '{JsonConvert.SerializeObject(player.Destructions)}'  WHERE userId = {player.Id}");
            }

            public static void Boosters(Player player)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                    mySqlClient.ExecuteNonQuery($"UPDATE player_equipment SET boosters = '{JsonConvert.SerializeObject(player.BoosterManager.Boosters)}' WHERE userId = {player.Id}");
            }

            public static void Position(Player player)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                    mySqlClient.ExecuteNonQuery($"UPDATE player_accounts SET lastPosition = '{JsonConvert.SerializeObject(player.LastPosition)}' WHERE userId = {player.Id}");
            }

            public static void Ammo(Player player)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                    mySqlClient.ExecuteNonQuery($"UPDATE player_accounts SET ammo = '{JsonConvert.SerializeObject(player.Ammo)}' WHERE userId = {player.Id}");
            }

            public static void Status(Player player)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                    mySqlClient.ExecuteNonQuery($"UPDATE player_accounts SET shipstatus = '{JsonConvert.SerializeObject(player.ShipStatus)}' WHERE userId = {player.Id}");
            }

            public static void Modules(Player player)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                    mySqlClient.ExecuteNonQuery($"UPDATE player_equipment SET modules = '{JsonConvert.SerializeObject(player.Storage.BattleStationModules)}' WHERE userId = {player.Id}");
            }

            public static bool Drones(Player player)
            {
                try
                {
                    using (var mySqlClient = SqlDatabaseManager.GetClient())
                    {
                        var querySet = mySqlClient.ExecuteQueryRow($"SELECT * FROM player_equipment WHERE userId = {player.Id}");
                        if (querySet == null)
                            return false;

                        var column = "";
                        if (querySet.Table.Columns.Contains("drone"))
                            column = "drone";
                        else if (querySet.Table.Columns.Contains("drones"))
                            column = "drones";

                        if (string.IsNullOrWhiteSpace(column))
                            return false;

                        var dronesJson = JsonConvert.SerializeObject(player.DroneManager?.DronesList ?? new List<Drones>());
                        dronesJson = dronesJson.Replace("'", "''");
                        mySqlClient.ExecuteNonQuery($"UPDATE player_equipment SET {column} = '{dronesJson}' WHERE userId = {player.Id}");
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("error_log", $"- [QueryManager.cs] SavePlayer.Drones({player.Id}) exception: {e}");
                    return false;
                }
            }
        }

        public class ChatFunctions
        {
            public static void AddBan(int bannedId, int modId, string reason, int typeId, string endDate)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    var result = (DataTable)mySqlClient.ExecuteQueryTable($"SELECT userId FROM player_accounts WHERE userId = {bannedId}");
                    if (result.Rows.Count >= 1)
                    {
                        mySqlClient.ExecuteNonQuery($"INSERT INTO server_bans (userId, modId, reason, typeId, end_date) VALUES ({bannedId}, {modId}, '{reason}', {typeId}, '{endDate}')");

                        GameManager.SendChatSystemMessage($"{QueryManager.GetUserPilotName(bannedId)} has banned.");
                    }
                }
            }

            public static void UnBan(int bannedId, int modId, int typeId)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    var result = (DataTable)mySqlClient.ExecuteQueryTable($"SELECT * FROM server_bans WHERE userId = {bannedId} AND typeId = {typeId}");
                    if (result.Rows.Count >= 1)
                    {
                        mySqlClient.ExecuteNonQuery($"UPDATE server_bans SET ended = 1 WHERE userId = {bannedId} AND typeId = {typeId}");

                        var client = GameManager.ChatClients[modId];

                        if (client != null)
                            client.Send($"{QueryManager.GetUserPilotName(bannedId)} has unbanned.");
                    }
                }
            }

            public static bool Banned(int userId)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    var result = (DataTable)mySqlClient.ExecuteQueryTable($"SELECT id FROM server_bans WHERE userId = {userId} AND typeId = 0 AND ended = 0");
                    return result.Rows.Count >= 1 ? true : false;
                }
            }
        }

        public static string GetUserPilotName(int userId)
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var result = mySqlClient.ExecuteQueryRow($"SELECT pilotName FROM player_accounts WHERE userId = {userId}");
                return result["pilotName"].ToString();
            }
        }

        public static bool CheckSessionId(int userId, string sessionId)
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var query = $"SELECT sessionId FROM player_accounts WHERE userId = {userId}";
                var table = (DataTable)mySqlClient.ExecuteQueryTable(query);

                if (table.Rows.Count >= 1)
                {
                    var result = mySqlClient.ExecuteQueryRow(query);
                    return sessionId == result["sessionId"].ToString();
                }
                else return false;
            }
        }

        public static bool Banned(int userId)
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var result = (DataTable)mySqlClient.ExecuteQueryTable($"SELECT id FROM server_bans WHERE userId = {userId} AND typeId = 1 AND ended = 0");
                return result.Rows.Count >= 1 ? true : false;
            }
        }

        public static Player GetPlayer(int playerId)
        {
            Player player = null;
            try
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    var data = mySqlClient.ExecuteQueryTable($"SELECT * FROM player_accounts WHERE userId = {playerId}");
                    foreach (DataRow row in data.Rows)
                    {
                        var name = Convert.ToString(row["pilotName"]);
                        var ship = GameManager.GetShip(Convert.ToInt32(row["shipId"]));
                        var factionId = Convert.ToInt32(row["factionId"]);
                        var rankId = Convert.ToInt32(row["rankID"]);
                        var warRank = Convert.ToInt32(row["warRank"]);
                        var clan = GameManager.GetClan(Convert.ToInt32(row["clanID"]));
                        var lastposition = JsonConvert.DeserializeObject<LastPosition>(row["lastPosition"].ToString());
                        var shipstatus = JsonConvert.DeserializeObject<ShipStatus>(row["shipstatus"].ToString());

                        player = new Player(playerId, name, clan, factionId, rankId, warRank, ship);
                        player.LastPosition = lastposition;
                        player.ShipStatus = shipstatus;
                        player.Premium = Convert.ToBoolean(row["premium"]);
                        player.Title = Convert.ToString(row["title"]);
                        player.Data = JsonConvert.DeserializeObject<DataBase>(row["data"].ToString());
                        player.Destructions = JsonConvert.DeserializeObject<DestructionsBase>(row["destructions"].ToString());
                        player.CurrentNanoHull = Convert.ToInt32(row["nanohull"]);
                        player.PetName = Convert.ToString(row["petName"]);
                    }

                    var settings = mySqlClient.ExecuteQueryTable($"SELECT * FROM player_settings WHERE userId = {playerId}");
                    foreach (DataRow row in settings.Rows)
                    {
                        if (row["audio"].ToString() != "")
                            player.Settings.Audio = JsonConvert.DeserializeObject<AudioBase>(row["audio"].ToString());
                        if (row["quality"].ToString() != "")
                            player.Settings.Quality = JsonConvert.DeserializeObject<QualityBase>(row["quality"].ToString());
                        if (row["classY2T"].ToString() != "")
                            player.Settings.ClassY2T = JsonConvert.DeserializeObject<ClassY2TBase>(row["classY2T"].ToString());
                        if (row["display"].ToString() != "")
                            player.Settings.Display = JsonConvert.DeserializeObject<DisplayBase>(row["display"].ToString());
                        if (row["gameplay"].ToString() != "")
                            player.Settings.Gameplay = JsonConvert.DeserializeObject<GameplayBase>(row["gameplay"].ToString());
                        if (row["window"].ToString() != "")
                            player.Settings.Window = JsonConvert.DeserializeObject<WindowBase>(row["window"].ToString());
                        if (row["inGameSettings"].ToString() != "")
                            player.Settings.InGameSettings = JsonConvert.DeserializeObject<InGameSettingsBase>(row["inGameSettings"].ToString());
                        if (row["cooldowns"].ToString() != "")
                            player.Settings.Cooldowns = JsonConvert.DeserializeObject<Dictionary<string, string>>(row["cooldowns"].ToString());
                        if (row["boundKeys"].ToString() != "")
                            player.Settings.BoundKeys = JsonConvert.DeserializeObject<List<BoundKeysBase>>(row["boundKeys"].ToString());
                        if (row["slotbarItems"].ToString() != "")
                            player.Settings.SlotBarItems = JsonConvert.DeserializeObject<Dictionary<short, string>>(row["slotbarItems"].ToString());
                        if (row["premiumSlotbarItems"].ToString() != "")
                            player.Settings.PremiumSlotBarItems = JsonConvert.DeserializeObject<Dictionary<short, string>>(row["premiumSlotbarItems"].ToString());
                        if (row["proActionBarItems"].ToString() != "")
                            player.Settings.ProActionBarItems = JsonConvert.DeserializeObject<Dictionary<short, string>>(row["proActionBarItems"].ToString());
                    }

                    var equipment = mySqlClient.ExecuteQueryTable($"SELECT * FROM player_equipment WHERE userId = {playerId}");
                    foreach (DataRow row in equipment.Rows)
                    {
                        player.BoosterManager.Boosters = JsonConvert.DeserializeObject<Dictionary<short, List<BoosterBase>>>(row["boosters"].ToString());
                        player.Storage.BattleStationModules = JsonConvert.DeserializeObject<List<ModuleBase>>(row["modules"].ToString());
                        player.SkillTree = JsonConvert.DeserializeObject<SkillTreeBase>(row["skill_points"].ToString());

                        dynamic items = JsonConvert.DeserializeObject(row["items"].ToString());

                        if (items["pet"] == "true")
                            player.Pet = new Pet(player);
                    }
                }

                SetEquipment(player);

                return player;
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [QueryManager.cs] GetPlayer({playerId}) exception: {e}");
                return null;
            }
        }

        public static void SetEquipment(Player player)
        {
            try
            {
                if (!TrySetEquipmentFromInventoryLoadout(player))
                {
                    var hp = player.Ship.BaseHitpoints + player.GetSkillPercentage("Ship Hull");
                    var baseSpeed = player.Ship.BaseSpeed + Maths.GetPercentage(player.Ship.BaseSpeed, 20);
                    player.equipedlasercount = 0;
                    player.fulllf3 = true;
                    player.AttackManager.RocketLauncher.MaxLoad = 0;
                    player.CurrentShieldAbsConfig1 = 0;
                    player.CurrentShieldAbsConfig2 = 0;
                    player.Equipment = new EquipmentBase(
                        new ConfigsBase(hp, 0, 0, baseSpeed, hp, 0, 0, baseSpeed, 0, 0, 0, 0),
                        new ItemsBase(0)
                    );
                }
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [QueryManager.cs] SetEquipment({player.Id}) exception: {e}");
            }
        }

        public static Dictionary<int, List<string>> GetEquippedItemsDebugByConfig(Player player)
        {
            Dictionary<int, List<string>> inventoryLoadoutDebug;
            if (TryGetEquippedItemsDebugByConfigFromInventoryLoadout(player, out inventoryLoadoutDebug))
                return inventoryLoadoutDebug;
            return new Dictionary<int, List<string>>
            {
                { 1, new List<string>() },
                { 2, new List<string>() }
            };
        }

        public static Dictionary<int, List<string>> GetDroneEquippedItemsDebugByConfig(Player player)
        {
            Dictionary<int, List<string>> inventoryLoadoutDebug;
            if (TryGetDroneEquippedItemsDebugByConfigFromInventoryLoadout(player, out inventoryLoadoutDebug))
                return inventoryLoadoutDebug;
            return new Dictionary<int, List<string>>
            {
                { 1, new List<string>() },
                { 2, new List<string>() }
            };
        }

        public static List<string> GetDroneLoadoutDebugDiagnostics(Player player)
        {
            var lines = new List<string>();
            if (player == null)
                return lines;

            try
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    var loadoutRows = TryGetTableByUser(mySqlClient, "player_inventory_loadout", player.Id);
                    if (loadoutRows == null)
                    {
                        lines.Add("loadout table not found/readable");
                        return lines;
                    }

                    var shipRows = 0;
                    var droneRows = 0;
                    var unknownRows = 0;
                    var rawOrigins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    foreach (DataRow row in loadoutRows.Rows)
                    {
                        var rawOrigin = ReadRowString(
                            row,
                            "mode",
                            "equipment_mode",
                            "equipmentMode",
                            "origin",
                            "loadout_origin",
                            "loadoutOrigin",
                            "equipment_origin",
                            "equipmentOrigin",
                            "target");

                        if (string.IsNullOrWhiteSpace(rawOrigin))
                            rawOrigin = "(empty)";

                        if (rawOrigins.ContainsKey(rawOrigin))
                            rawOrigins[rawOrigin] += 1;
                        else
                            rawOrigins[rawOrigin] = 1;

                        var parsed = ParseLoadoutOrigin(row);
                        if (parsed == LoadoutOrigin.Ship)
                            shipRows++;
                        else if (parsed == LoadoutOrigin.Drone)
                            droneRows++;
                        else
                            unknownRows++;
                    }

                    lines.Add($"loadout rows: total={loadoutRows.Rows.Count}, ship={shipRows}, drone={droneRows}, unknown={unknownRows}");
                    foreach (var kv in rawOrigins.OrderBy(x => x.Key))
                        lines.Add($"raw origin '{kv.Key}' x{kv.Value}");
                }
            }
            catch (Exception e)
            {
                lines.Add("diagnostics exception: " + e.Message);
            }

            return lines;
        }

        private enum LoadoutItemKind
        {
            Unknown = 0,
            Lf1,
            Mp1,
            Lf2,
            Lf3,
            Lf4,
            Hst1,
            Hst2,
            Bo2,
            G3n7900,
            G3n6900,
            G3n3310,
            G3n3210,
            G3n2010,
            G3n1010,
            Ao1,
            Ao2,
            Ao3,
            Bo1,
            Havoc,
            Hercules
        }

        private class ResolvedLoadoutItem
        {
            public int Config;
            public int ItemId;
            public LoadoutItemKind Kind;
            public LoadoutOrigin Origin;
        }

        private enum LoadoutOrigin
        {
            Unknown = 0,
            Ship = 1,
            Drone = 2
        }

        private static bool TrySetEquipmentFromInventoryLoadout(Player player)
        {
            List<ResolvedLoadoutItem> equippedItems;
            if (!TryGetResolvedShipLoadoutItems(player, out equippedItems))
                return false;

            const int lf1Damage = 40;
            const int mp1Damage = 60;
            const int lf2Damage = 100;
            const int lf3Damage = 150;
            const int lf4Damage = 200;
            const int bo2Shield = 10000;
            const int bo2Shieldabs = 80;
            const int bo1Shield = 4000;
            const int bo1Shieldabs = 70;
            const int ao1Shield = 1000;
            const int ao1Shieldabs = 40;
            const int ao2Shield = 2000;
            const int ao2Shieldabs = 50;
            const int ao3Shield = 5000;
            const int ao3Shieldabs = 60;
            const int g3n7900Speed = 10;
            const int g3n6900Speed = 7;
            const int g3n3310Speed = 5;
            const int g3n3210Speed = 4;
            const int g3n2010Speed = 3;
            const int g3n1010Speed = 2;

            var hitpoints = new int[] { player.Ship.BaseHitpoints + player.GetSkillPercentage("Ship Hull"), player.Ship.BaseHitpoints + player.GetSkillPercentage("Ship Hull") };
            var speed = new int[] { player.Ship.BaseSpeed, player.Ship.BaseSpeed };
            var damage = new int[] { 0, 0 };
            var shield = new int[] { 0, 0 };
            var equipedshieldcount = new int[] { 0, 0 };
            var shieldabsorption = new int[] { 0, 0 };
            var leonovlaser = new int[] { 0, 0 };
            var leonovshield = new int[] { 0, 0 };

            player.equipedlasercount = 0;
            player.fulllf3 = true;
            player.AttackManager.RocketLauncher.MaxLoad = 0;

            foreach (var item in equippedItems)
            {
                var configIndex = item.Config - 1;
                if (configIndex < 0 || configIndex > 1)
                    continue;

                if (item.Origin == LoadoutOrigin.Drone && !IsDroneLoadoutKindAllowed(item.Kind))
                    continue;

                switch (item.Kind)
                {
                    case LoadoutItemKind.Lf3:
                        damage[configIndex] += lf3Damage;
                        leonovlaser[configIndex] += lf3Damage;
                        player.equipedlasercount++;
                        break;
                    case LoadoutItemKind.Lf4:
                        damage[configIndex] += lf4Damage;
                        leonovlaser[configIndex] += lf4Damage;
                        player.fulllf3 = false;
                        player.equipedlasercount++;
                        break;
                    case LoadoutItemKind.Lf1:
                        damage[configIndex] += lf1Damage;
                        leonovlaser[configIndex] += lf1Damage;
                        player.fulllf3 = false;
                        player.equipedlasercount++;
                        break;
                    case LoadoutItemKind.Mp1:
                        damage[configIndex] += mp1Damage;
                        leonovlaser[configIndex] += mp1Damage;
                        player.fulllf3 = false;
                        player.equipedlasercount++;
                        break;
                    case LoadoutItemKind.Lf2:
                        damage[configIndex] += lf2Damage;
                        leonovlaser[configIndex] += lf2Damage;
                        player.fulllf3 = false;
                        player.equipedlasercount++;
                        break;
                    case LoadoutItemKind.Hst1:
                        player.AttackManager.RocketLauncher.MaxLoad += 3;
                        break;
                    case LoadoutItemKind.Hst2:
                        player.AttackManager.RocketLauncher.MaxLoad += 5;
                        break;
                    case LoadoutItemKind.Bo2:
                        shield[configIndex] += bo2Shield;
                        shieldabsorption[configIndex] += bo2Shieldabs;
                        equipedshieldcount[configIndex]++;
                        leonovshield[configIndex] += bo2Shield;
                        break;
                    case LoadoutItemKind.G3n7900:
                        speed[configIndex] += g3n7900Speed;
                        break;
                    case LoadoutItemKind.G3n6900:
                        speed[configIndex] += g3n6900Speed;
                        break;
                    case LoadoutItemKind.G3n3310:
                        speed[configIndex] += g3n3310Speed;
                        break;
                    case LoadoutItemKind.G3n3210:
                        speed[configIndex] += g3n3210Speed;
                        break;
                    case LoadoutItemKind.G3n2010:
                        speed[configIndex] += g3n2010Speed;
                        break;
                    case LoadoutItemKind.G3n1010:
                        speed[configIndex] += g3n1010Speed;
                        break;
                    case LoadoutItemKind.Ao1:
                        shield[configIndex] += ao1Shield;
                        shieldabsorption[configIndex] += ao1Shieldabs;
                        equipedshieldcount[configIndex]++;
                        break;
                    case LoadoutItemKind.Ao2:
                        shield[configIndex] += ao2Shield;
                        shieldabsorption[configIndex] += ao2Shieldabs;
                        equipedshieldcount[configIndex]++;
                        break;
                    case LoadoutItemKind.Ao3:
                        shield[configIndex] += ao3Shield;
                        shieldabsorption[configIndex] += ao3Shieldabs;
                        equipedshieldcount[configIndex]++;
                        break;
                    case LoadoutItemKind.Bo1:
                        shield[configIndex] += bo1Shield;
                        shieldabsorption[configIndex] += bo1Shieldabs;
                        equipedshieldcount[configIndex]++;
                        break;
                }
            }

            var droneDamageBonusPercent = 0;
            var droneShieldBonusPercent = 0;
            if (player.DroneManager != null)
                player.DroneManager.GetActiveDroneLevelBonus(out droneDamageBonusPercent, out droneShieldBonusPercent);
            if (droneDamageBonusPercent > 0)
            {
                damage[0] += Maths.GetPercentage(damage[0], droneDamageBonusPercent);
                damage[1] += Maths.GetPercentage(damage[1], droneDamageBonusPercent);
            }

            if (droneShieldBonusPercent > 0)
            {
                shield[0] += Maths.GetPercentage(shield[0], droneShieldBonusPercent);
                shield[1] += Maths.GetPercentage(shield[1], droneShieldBonusPercent);
            }

            speed[0] += Maths.GetPercentage(speed[0], 20);
            speed[1] += Maths.GetPercentage(speed[1], 20);
            player.CurrentShieldAbsConfig1 = shieldabsorption[0] / (equipedshieldcount[0] == 0 ? 1 : equipedshieldcount[0]);
            player.CurrentShieldAbsConfig2 = shieldabsorption[1] / (equipedshieldcount[1] == 0 ? 1 : equipedshieldcount[1]);

            var configsBase = new ConfigsBase(hitpoints[0], damage[0], shield[0], speed[0], hitpoints[1], damage[1], shield[1], speed[1], leonovlaser[0], leonovlaser[1], leonovshield[0], leonovshield[1]);
            var itemsBase = new ItemsBase(0);
            player.Equipment = new EquipmentBase(configsBase, itemsBase);

            if (player.AttackManager.RocketLauncher.CurrentLoad > player.AttackManager.RocketLauncher.MaxLoad)
                player.AttackManager.RocketLauncher.CurrentLoad = player.AttackManager.RocketLauncher.MaxLoad;

            return true;
        }

        private static bool TryGetEquippedItemsDebugByConfigFromInventoryLoadout(Player player, out Dictionary<int, List<string>> result)
        {
            result = null;

            List<ResolvedLoadoutItem> equippedItems;
            if (!TryGetResolvedShipLoadoutItems(player, out equippedItems))
                return false;

            var config1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var config2 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in equippedItems)
            {
                var summary = item.Config == 2 ? config2 : config1;
                var name = ResolveDebugItemName(item);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (summary.ContainsKey(name))
                    summary[name] += 1;
                else
                    summary[name] = 1;
            }

            result = new Dictionary<int, List<string>>
            {
                { 1, BuildDebugLines(config1) },
                { 2, BuildDebugLines(config2) }
            };

            return true;
        }

        private static bool TryGetDroneEquippedItemsDebugByConfigFromInventoryLoadout(Player player, out Dictionary<int, List<string>> result)
        {
            result = null;

            List<ResolvedLoadoutItem> equippedItems;
            if (!TryGetResolvedShipLoadoutItems(player, out equippedItems))
                return false;

            var config1 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var config2 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in equippedItems.Where(x => x.Origin == LoadoutOrigin.Drone))
            {
                var summary = item.Config == 2 ? config2 : config1;
                var name = ResolveDebugItemName(item);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (summary.ContainsKey(name))
                    summary[name] += 1;
                else
                    summary[name] = 1;
            }

            result = new Dictionary<int, List<string>>
            {
                { 1, BuildDebugLines(config1) },
                { 2, BuildDebugLines(config2) }
            };

            return true;
        }

        private static bool TryGetResolvedShipLoadoutItems(Player player, out List<ResolvedLoadoutItem> equippedItems)
        {
            equippedItems = new List<ResolvedLoadoutItem>();

            try
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    var loadoutRows = TryGetTableByUser(mySqlClient, "player_inventory_loadout", player.Id);
                    if (loadoutRows == null)
                        return false;

                    var inventoryRows = TryGetTableByUser(mySqlClient, "player_inventory_items", player.Id);
                    var ownedCounts = BuildOwnedItemCount(inventoryRows);
                    var knownKinds = BuildKnownItemKinds(mySqlClient, inventoryRows);
                    var selectedSlots = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);

                    foreach (DataRow row in loadoutRows.Rows)
                    {
                        var origin = ParseLoadoutOrigin(row);
                        if (origin == LoadoutOrigin.Unknown)
                            continue;

                        var config = ParseConfigId(row);
                        if (config != 1 && config != 2)
                            continue;

                        var itemId = ParseItemId(row);
                        if (itemId < 0)
                            continue;

                        var slotGroup = ReadRowString(row, "slot_group", "slotGroup", "group", "category");
                        var slotIndex = ParseInt(row, 0, "slot_index", "slotIndex", "slot", "position");
                        var key = config + "|" + origin + "|" + slotGroup + "|" + slotIndex;
                        selectedSlots[key] = row;
                    }

                    var equippedPerItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var orderedRows = selectedSlots.Values
                        .OrderBy(x => ParseLoadoutOrigin(x) == LoadoutOrigin.Ship ? 0 : 1)
                        .ToList();

                    foreach (DataRow slotRow in orderedRows)
                    {
                        var itemId = ParseItemId(slotRow);
                        if (itemId < 0)
                            continue;

                        var config = ParseConfigId(slotRow);
                        var origin = ParseLoadoutOrigin(slotRow);
                        if (origin == LoadoutOrigin.Unknown)
                            continue;

                        var slotGroup = ReadRowString(slotRow, "slot_group", "slotGroup", "group", "category");
                        var equipCountKey = config + "|" + slotGroup + "|" + itemId;

                        if (ownedCounts.Count > 0)
                        {
                            int ownedCount;
                            if (!ownedCounts.TryGetValue(itemId, out ownedCount) || ownedCount <= 0)
                                continue;

                            int equippedCount;
                            if (!equippedPerItem.TryGetValue(equipCountKey, out equippedCount))
                                equippedCount = 0;

                            if (equippedCount >= ownedCount)
                                continue;
                        }

                        var kind = ResolveLoadoutItemKind(slotRow, itemId, knownKinds);

                        int currentCount;
                        if (!equippedPerItem.TryGetValue(equipCountKey, out currentCount))
                            currentCount = 0;
                        equippedPerItem[equipCountKey] = currentCount + 1;

                        equippedItems.Add(new ResolvedLoadoutItem
                        {
                            Config = config,
                            ItemId = itemId,
                            Kind = kind,
                            Origin = origin
                        });
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [QueryManager.cs] TryGetResolvedShipLoadoutItems({player.Id}) exception: {e}");
                return false;
            }
        }

        private static DataTable TryGetTableByUser(dynamic mySqlClient, string tableName, int userId)
        {
            try
            {
                var sample = mySqlClient.ExecuteQueryTable($"SELECT * FROM {tableName} LIMIT 1") as DataTable;
                if (sample == null)
                    return null;

                string userColumn = null;
                foreach (var candidate in new[] { "user_id", "userId", "userid" })
                {
                    if (sample.Columns.Contains(candidate))
                    {
                        userColumn = candidate;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(userColumn))
                    return null;

                return mySqlClient.ExecuteQueryTable($"SELECT * FROM {tableName} WHERE {userColumn} = {userId}") as DataTable;
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<int, int> BuildOwnedItemCount(DataTable inventoryRows)
        {
            var result = new Dictionary<int, int>();
            if (inventoryRows == null)
                return result;

            foreach (DataRow row in inventoryRows.Rows)
            {
                var itemId = ParseInt(row, 0, "item_id", "itemId");
                if (itemId < 0)
                    continue;

                var count = ParseInt(row, 1, "amount", "count", "quantity", "qty", "owned_count", "stack");
                if (count < 1)
                    count = 1;

                if (result.ContainsKey(itemId))
                    result[itemId] += count;
                else
                    result[itemId] = count;
            }

            return result;
        }

        private static Dictionary<int, LoadoutItemKind> BuildKnownItemKinds(dynamic mySqlClient, DataTable inventoryRows)
        {
            var result = new Dictionary<int, LoadoutItemKind>();

            // Canonical definitions table used by CMS and gameserver.
            var canonicalTables = new[] { "server_item_definitions", "inventory_item_definitions" };
            foreach (var definitionTable in canonicalTables)
            {
                try
                {
                    var table = mySqlClient.ExecuteQueryTable($"SELECT * FROM {definitionTable}") as DataTable;
                    if (table == null || table.Rows.Count == 0)
                        continue;

                    foreach (DataRow row in table.Rows)
                    {
                        var itemId = ParseInt(row, -1, "item_id", "id", "itemId");
                        if (itemId < 0 || result.ContainsKey(itemId))
                            continue;

                        var isActive = ParseInt(row, 1, "is_active", "isActive", "active");
                        if (isActive == 0)
                            continue;

                        var kind = ResolveKindFromDefinitionRow(row, itemId);
                        if (kind != LoadoutItemKind.Unknown)
                            result[itemId] = kind;
                    }
                }
                catch
                {
                    // Table not available in this schema version.
                }
            }

            if (inventoryRows != null)
            {
                foreach (DataRow row in inventoryRows.Rows)
                {
                    var itemId = ParseInt(row, 0, "item_id", "itemId");
                    if (itemId < 0 || result.ContainsKey(itemId))
                        continue;

                    var kind = ResolveKindFromText(ReadRowString(row, "loot_id", "lootId", "item_loot_id", "itemLootId", "code", "name", "type"));
                    if (kind != LoadoutItemKind.Unknown)
                        result[itemId] = kind;
                }
            }

            var definitionTables = new[] { "inventory_items", "player_inventory_catalog", "server_inventory_items" };
            foreach (var definitionTable in definitionTables)
            {
                try
                {
                    var table = mySqlClient.ExecuteQueryTable($"SELECT * FROM {definitionTable}") as DataTable;
                    if (table == null || table.Rows.Count == 0)
                        continue;

                    foreach (DataRow row in table.Rows)
                    {
                        var itemId = ParseInt(row, 0, "item_id", "id", "itemId");
                        if (itemId < 0 || result.ContainsKey(itemId))
                            continue;

                        var mergedText = string.Join(" ",
                            ReadRowString(row, "loot_id", "lootId", "item_loot_id", "itemLootId"),
                            ReadRowString(row, "type", "item_type", "itemType", "category"),
                            ReadRowString(row, "name", "title"));
                        var kind = ResolveKindFromText(mergedText);
                        if (kind != LoadoutItemKind.Unknown)
                            result[itemId] = kind;
                    }
                }
                catch
                {
                    // Table is optional depending on CMS schema/version.
                }
            }

            return result;
        }

        private static LoadoutItemKind ResolveKindFromDefinitionRow(DataRow row, int itemId)
        {
            var legacyKey = ReadRowString(row, "legacy_count_key", "legacyCountKey");
            var category = ReadRowString(row, "category", "item_type", "itemType", "type");
            var name = ReadRowString(row, "name", "title");
            var shopName = ReadRowString(row, "shop_name", "shopName");

            var byLegacy = ResolveKindFromLegacyKey(legacyKey);
            if (byLegacy != LoadoutItemKind.Unknown)
                return byLegacy;

            var byText = ResolveKindFromText(string.Join(" ", name, shopName, category));
            if (byText != LoadoutItemKind.Unknown)
                return byText;

            return ResolveKindFromItemId(itemId);
        }

        private static LoadoutItemKind ResolveKindFromLegacyKey(string legacyKey)
        {
            if (string.IsNullOrWhiteSpace(legacyKey))
                return LoadoutItemKind.Unknown;

            switch (legacyKey.Trim())
            {
                case "lf1Count":
                    return LoadoutItemKind.Lf1;
                case "mp1Count":
                    return LoadoutItemKind.Mp1;
                case "lf2Count":
                    return LoadoutItemKind.Lf2;
                case "lf3Count":
                    return LoadoutItemKind.Lf3;
                case "lf4Count":
                    return LoadoutItemKind.Lf4;
                case "hs1Count":
                    return LoadoutItemKind.Hst1;
                case "hs2Count":
                    return LoadoutItemKind.Hst2;
                case "bo2Count":
                    return LoadoutItemKind.Bo2;
                case "g3n7900Count":
                    return LoadoutItemKind.G3n7900;
                case "g3n6900Count":
                    return LoadoutItemKind.G3n6900;
                case "g3n3310Count":
                    return LoadoutItemKind.G3n3310;
                case "g3n3210Count":
                    return LoadoutItemKind.G3n3210;
                case "g3n2010Count":
                    return LoadoutItemKind.G3n2010;
                case "g3n1010Count":
                    return LoadoutItemKind.G3n1010;
                case "ao1Count":
                    return LoadoutItemKind.Ao1;
                case "ao2Count":
                    return LoadoutItemKind.Ao2;
                case "ao3Count":
                    return LoadoutItemKind.Ao3;
                case "bo1Count":
                    return LoadoutItemKind.Bo1;
                case "havocCount":
                    return LoadoutItemKind.Havoc;
                case "herculesCount":
                    return LoadoutItemKind.Hercules;
                default:
                    return LoadoutItemKind.Unknown;
            }
        }

        private static LoadoutItemKind ResolveKindFromItemId(int itemId)
        {
            switch (itemId)
            {
                case 0:
                    return LoadoutItemKind.Bo2;
                case 1:
                    return LoadoutItemKind.G3n7900;
                case 5:
                    return LoadoutItemKind.Havoc;
                case 6:
                    return LoadoutItemKind.Hercules;
                case 7:
                    return LoadoutItemKind.Lf3;
                case 8:
                    return LoadoutItemKind.Lf4;
                case 123:
                    return LoadoutItemKind.Lf1;
                case 124:
                    return LoadoutItemKind.Mp1;
                case 125:
                    return LoadoutItemKind.Lf2;
                case 126:
                    return LoadoutItemKind.G3n6900;
                case 127:
                    return LoadoutItemKind.G3n3310;
                case 128:
                    return LoadoutItemKind.G3n3210;
                case 129:
                    return LoadoutItemKind.G3n2010;
                case 130:
                    return LoadoutItemKind.G3n1010;
                case 131:
                    return LoadoutItemKind.Ao1;
                case 132:
                    return LoadoutItemKind.Ao2;
                case 133:
                    return LoadoutItemKind.Ao3;
                case 134:
                    return LoadoutItemKind.Bo1;
                case 135:
                    return LoadoutItemKind.Hst1;
                case 136:
                    return LoadoutItemKind.Hst2;
                default:
                    return LoadoutItemKind.Unknown;
            }
        }

        private static LoadoutItemKind ResolveLoadoutItemKind(DataRow loadoutRow, int itemId, Dictionary<int, LoadoutItemKind> knownKinds)
        {
            LoadoutItemKind kind;
            if (knownKinds.TryGetValue(itemId, out kind))
                return kind;

            kind = ResolveKindFromText(ReadRowString(loadoutRow, "item_code", "itemCode", "loot_id", "lootId", "item_loot_id", "itemLootId", "type", "category"));
            return kind;
        }

        private static LoadoutOrigin ParseLoadoutOrigin(DataRow row)
        {
            var mode = ReadRowString(
                row,
                "mode",
                "equipment_mode",
                "equipmentMode",
                "origin",
                "loadout_origin",
                "loadoutOrigin",
                "equipment_origin",
                "equipmentOrigin",
                "target");

            if (string.IsNullOrWhiteSpace(mode))
            {
                foreach (DataColumn column in row.Table.Columns)
                {
                    var value = row[column];
                    if (value == null || value == DBNull.Value)
                        continue;

                    var parsed = ParseLoadoutOriginValue(value.ToString());
                    if (parsed != LoadoutOrigin.Unknown)
                        return parsed;
                }

                return LoadoutOrigin.Ship;
            }

            var parsedMode = ParseLoadoutOriginValue(mode);
            if (parsedMode != LoadoutOrigin.Unknown)
                return parsedMode;

            return LoadoutOrigin.Unknown;
        }

        private static LoadoutOrigin ParseLoadoutOriginValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LoadoutOrigin.Unknown;

            var normalized = value.Trim().ToLowerInvariant();
            if (normalized == "ship" || normalized == "ships")
                return LoadoutOrigin.Ship;

            if (normalized == "drone" || normalized == "drones")
                return LoadoutOrigin.Drone;

            if (normalized == "0")
                return LoadoutOrigin.Ship;

            if (normalized == "1")
                return LoadoutOrigin.Drone;

            if (normalized == "2")
                return LoadoutOrigin.Drone;

            return LoadoutOrigin.Unknown;
        }

        private static int ParseConfigId(DataRow row)
        {
            var config = ParseInt(row, 1, "config_id", "configId", "config", "configuration");
            if (config < 1)
                config = 1;
            if (config > 2)
                config = 2;
            return config;
        }

        private static int ParseItemId(DataRow row)
        {
            return ParseInt(row, -1, "item_id", "itemId", "inventory_item_id", "inventoryItemId");
        }

        private static int ParseInt(DataRow row, int fallback, params string[] columns)
        {
            foreach (var col in columns)
            {
                if (!row.Table.Columns.Contains(col))
                    continue;

                try
                {
                    if (row[col] == null || row[col] == DBNull.Value)
                        continue;

                    int value;
                    if (int.TryParse(row[col].ToString(), out value))
                        return value;
                }
                catch { }
            }

            return fallback;
        }

        private static string ReadRowString(DataRow row, params string[] columns)
        {
            foreach (var col in columns)
            {
                if (!row.Table.Columns.Contains(col))
                    continue;

                try
                {
                    if (row[col] == null || row[col] == DBNull.Value)
                        continue;

                    var value = row[col].ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                catch { }
            }

            return "";
        }

        private static LoadoutItemKind ResolveKindFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return LoadoutItemKind.Unknown;

            var normalized = text.Trim().ToLowerInvariant();

            if (normalized.Contains("lf-4") || normalized.Contains("lf4"))
                return LoadoutItemKind.Lf4;
            if (normalized.Contains("lf-3") || normalized.Contains("lf3"))
                return LoadoutItemKind.Lf3;
            if (normalized.Contains("lf-2") || normalized.Contains("lf2"))
                return LoadoutItemKind.Lf2;
            if (normalized.Contains("lf-1") || normalized.Contains("lf1"))
                return LoadoutItemKind.Lf1;
            if (normalized.Contains("mp-1") || normalized.Contains("mp1"))
                return LoadoutItemKind.Mp1;
            if (normalized.Contains("hst-2") || normalized.Contains("hst2"))
                return LoadoutItemKind.Hst2;
            if (normalized.Contains("hst-1") || normalized.Contains("hst1"))
                return LoadoutItemKind.Hst1;
            if (normalized.Contains("sg3n-bo2") || normalized.Contains("bo2"))
                return LoadoutItemKind.Bo2;
            if (normalized.Contains("g3n-7900") || normalized.Contains("g3n7900"))
                return LoadoutItemKind.G3n7900;
            if (normalized.Contains("g3n-6900") || normalized.Contains("g3n6900"))
                return LoadoutItemKind.G3n6900;
            if (normalized.Contains("g3n-3310") || normalized.Contains("g3n3310"))
                return LoadoutItemKind.G3n3310;
            if (normalized.Contains("g3n-3210") || normalized.Contains("g3n3210"))
                return LoadoutItemKind.G3n3210;
            if (normalized.Contains("g3n-2010") || normalized.Contains("g3n2010"))
                return LoadoutItemKind.G3n2010;
            if (normalized.Contains("g3n-1010") || normalized.Contains("g3n1010"))
                return LoadoutItemKind.G3n1010;
            if (normalized.Contains("sg3n-ao1") || normalized.Contains("ao1"))
                return LoadoutItemKind.Ao1;
            if (normalized.Contains("sg3n-ao2") || normalized.Contains("ao2"))
                return LoadoutItemKind.Ao2;
            if (normalized.Contains("sg3n-ao3") || normalized.Contains("ao3"))
                return LoadoutItemKind.Ao3;
            if (normalized.Contains("sg3n-bo1") || normalized.Contains("bo1"))
                return LoadoutItemKind.Bo1;
            if (normalized.Contains("havoc"))
                return LoadoutItemKind.Havoc;
            if (normalized.Contains("hercules"))
                return LoadoutItemKind.Hercules;

            return LoadoutItemKind.Unknown;
        }

        private static string GetLoadoutItemName(LoadoutItemKind kind)
        {
            switch (kind)
            {
                case LoadoutItemKind.Lf3:
                    return "LF-3";
                case LoadoutItemKind.Lf4:
                    return "LF-4";
                case LoadoutItemKind.Lf1:
                    return "LF-1";
                case LoadoutItemKind.Mp1:
                    return "MP-1";
                case LoadoutItemKind.Lf2:
                    return "LF-2";
                case LoadoutItemKind.Hst1:
                    return "HST-1";
                case LoadoutItemKind.Hst2:
                    return "HST-2";
                case LoadoutItemKind.Bo2:
                    return "SG3N-BO2";
                case LoadoutItemKind.G3n7900:
                    return "G3N-7900";
                case LoadoutItemKind.G3n6900:
                    return "G3N-6900";
                case LoadoutItemKind.G3n3310:
                    return "G3N-3310";
                case LoadoutItemKind.G3n3210:
                    return "G3N-3210";
                case LoadoutItemKind.G3n2010:
                    return "G3N-2010";
                case LoadoutItemKind.G3n1010:
                    return "G3N-1010";
                case LoadoutItemKind.Ao1:
                    return "SG3N-AO1";
                case LoadoutItemKind.Ao2:
                    return "SG3N-AO2";
                case LoadoutItemKind.Ao3:
                    return "SG3N-AO3";
                case LoadoutItemKind.Bo1:
                    return "SG3N-BO1";
                case LoadoutItemKind.Havoc:
                    return "Havoc";
                case LoadoutItemKind.Hercules:
                    return "Hercules";
                default:
                    return null;
            }
        }

        private static string ResolveDebugItemName(ResolvedLoadoutItem item)
        {
            // Migrated legacy debug helpers: same semantic groups, now driven by canonical item_id/kind.
            return GetLaserNameForShip(item.ItemId, item.Kind)
                ?? GetRocketLauncherName(item.ItemId, item.Kind)
                ?? GetGeneratorName(item.ItemId, item.Kind)
                ?? GetDroneDesignName(item.ItemId, item.Kind)
                ?? GetDroneItemName(item.ItemId, item.Kind)
                ?? $"UNKNOWN-ITEM({item.ItemId})";
        }

        private static void AddItem(Dictionary<string, int> summary, string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return;

            if (summary.ContainsKey(itemName))
                summary[itemName] += 1;
            else
                summary[itemName] = 1;
        }

        private static List<string> BuildDebugLines(Dictionary<string, int> summary)
        {
            var lines = new List<string>();
            var order = new List<string>
            {
                "LF-3",
                "LF-4",
                "LF-1",
                "MP-1",
                "LF-2",
                "HST-1",
                "HST-2",
                "SG3N-BO2",
                "G3N-7900",
                "G3N-6900",
                "G3N-3310",
                "G3N-3210",
                "G3N-2010",
                "G3N-1010",
                "SG3N-AO1",
                "SG3N-AO2",
                "SG3N-AO3",
                "SG3N-BO1",
                "Havoc",
                "Hercules"
            };

            foreach (var name in order)
            {
                if (summary.TryGetValue(name, out var count) && count > 0)
                    lines.Add($"{name} x{count}");
            }

            // Keep unknown/unordered entries visible for site-vs-server mismatch checks.
            foreach (var kv in summary.Where(x => !order.Contains(x.Key)).OrderBy(x => x.Key))
            {
                if (kv.Value > 0)
                    lines.Add($"{kv.Key} x{kv.Value}");
            }

            return lines;
        }

        private static List<int> ReadIntList(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<int>();

            try
            {
                return JsonConvert.DeserializeObject<List<int>>(json) ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        private static JArray ReadDroneArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new JArray();

            try
            {
                return JArray.Parse(json);
            }
            catch
            {
                return new JArray();
            }
        }

        private static string GetLaserNameForShip(int itemId, LoadoutItemKind kind)
        {
            if (kind == LoadoutItemKind.Lf3)
                return "LF-3";
            if (kind == LoadoutItemKind.Lf4)
                return "LF-4";
            if (kind == LoadoutItemKind.Lf1)
                return "LF-1";
            if (kind == LoadoutItemKind.Mp1)
                return "MP-1";
            if (kind == LoadoutItemKind.Lf2)
                return "LF-2";

            return null;
        }

        private static string GetRocketLauncherName(int itemId, LoadoutItemKind kind)
        {
            if (kind == LoadoutItemKind.Hst1)
                return "HST-1";
            if (kind == LoadoutItemKind.Hst2)
                return "HST-2";

            return null;
        }

        private static string GetGeneratorName(int itemId, LoadoutItemKind kind)
        {
            if (kind == LoadoutItemKind.Bo2)
                return "SG3N-BO2";
            if (kind == LoadoutItemKind.G3n7900)
                return "G3N-7900";
            if (kind == LoadoutItemKind.G3n6900)
                return "G3N-6900";
            if (kind == LoadoutItemKind.G3n3310)
                return "G3N-3310";
            if (kind == LoadoutItemKind.G3n3210)
                return "G3N-3210";
            if (kind == LoadoutItemKind.G3n2010)
                return "G3N-2010";
            if (kind == LoadoutItemKind.G3n1010)
                return "G3N-1010";
            if (kind == LoadoutItemKind.Ao1)
                return "SG3N-AO1";
            if (kind == LoadoutItemKind.Ao2)
                return "SG3N-AO2";
            if (kind == LoadoutItemKind.Ao3)
                return "SG3N-AO3";
            if (kind == LoadoutItemKind.Bo1)
                return "SG3N-BO1";

            return null;
        }

        private static string GetDroneDesignName(int designId, LoadoutItemKind kind)
        {
            if (kind == LoadoutItemKind.Havoc)
                return "Havoc";
            if (kind == LoadoutItemKind.Hercules)
                return "Hercules";

            return null;
        }

        private static string GetDroneItemName(int itemId, LoadoutItemKind kind)
        {
            // New backend has drones disabled for equipment mode=ship; keep method for parity.
            return null;
        }

        private static bool IsDroneLoadoutKindAllowed(LoadoutItemKind kind)
        {
            switch (kind)
            {
                case LoadoutItemKind.Lf1:
                case LoadoutItemKind.Mp1:
                case LoadoutItemKind.Lf2:
                case LoadoutItemKind.Lf3:
                case LoadoutItemKind.Lf4:
                case LoadoutItemKind.Bo2:
                case LoadoutItemKind.Ao1:
                case LoadoutItemKind.Ao2:
                case LoadoutItemKind.Ao3:
                case LoadoutItemKind.Bo1:
                    return true;
                case LoadoutItemKind.Hst1:
                case LoadoutItemKind.Hst2:
                    return false;
                case LoadoutItemKind.G3n7900:
                case LoadoutItemKind.G3n6900:
                case LoadoutItemKind.G3n3310:
                case LoadoutItemKind.G3n3210:
                case LoadoutItemKind.G3n2010:
                case LoadoutItemKind.G3n1010:
                    return false;
                default:
                    return false;
            }
        }

        public static void LoadMaps()
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var data = (DataTable)mySqlClient.ExecuteQueryTable("SELECT * FROM server_maps");
                foreach (DataRow row in data.Rows)
                {
                    int mapId = Convert.ToInt32(row["mapID"]);
                    string name = Convert.ToString(row["name"]);
                    int factionId = Convert.ToInt32(row["factionID"]);
                    var npcs = JsonConvert.DeserializeObject<List<NpcsBase>>(row["npcs"].ToString());
                    var portals = JsonConvert.DeserializeObject<List<PortalBase>>(row["portals"].ToString());
                    var stations = JsonConvert.DeserializeObject<List<StationBase>>(row["stations"].ToString());
                    var options = JsonConvert.DeserializeObject<OptionsBase>(row["options"].ToString());
                    var spacemap = new Spacemap(mapId, name, factionId, npcs, portals, stations, options);
                    GameManager.Spacemaps.TryAdd(spacemap.Id, spacemap);
                }
            }

            LoadBattleStations();
        }


        public class BattleStations
        {
            public static void BattleStation(BattleStation battleStation)
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    var visualModifiers = new List<int>();

                    foreach (var modifier in battleStation.VisualModifiers.Keys)
                        visualModifiers.Add(modifier);

                    var buildTime = battleStation.AssetTypeId != AssetTypeModule.BATTLESTATION && battleStation.InBuildingState ? $"buildTime = '{battleStation.buildTime.ToString("yyyy-MM-dd HH:mm:ss")}'," : "";
                    var deflectorTime = !battleStation.DeflectorActive ? $"deflectorTime = '{battleStation.deflectorTime.ToString("yyyy-MM-dd HH:mm:ss")}'," : "";

                    mySqlClient.ExecuteNonQuery($"UPDATE server_battlestations SET clanId = {battleStation.Clan.Id}," +
                    $"inBuildingState = {battleStation.InBuildingState}, buildTimeInMinutes = {battleStation.BuildTimeInMinutes}, {buildTime}" +
                    $"deflectorActive = {battleStation.DeflectorActive}, deflectorSecondsLeft = {battleStation.DeflectorSecondsLeft}, {deflectorTime} visualModifiers = '{JsonConvert.SerializeObject(visualModifiers)}' WHERE name = '{battleStation.AsteroidName}'");
                }
            }

            public static void Modules(BattleStation battleStation)
            {
                var modules = new List<EquippedModuleBase>();

                foreach (var equipped in battleStation.EquippedStationModule)
                {
                    var module = new List<SatelliteBase>();

                    foreach (var equippedModule in battleStation.EquippedStationModule[equipped.Key])
                    {
                        module.Add(new SatelliteBase(equippedModule.OwnerId, equippedModule.ItemId, equippedModule.SlotId, equippedModule.DesignId, equippedModule.Type, equippedModule.CurrentHitPoints,
                            equippedModule.MaxHitPoints, equippedModule.CurrentShieldPoints, equippedModule.MaxShieldPoints, equippedModule.InstallationSecondsLeft, equippedModule.Installed));
                    }

                    modules.Add(new EquippedModuleBase(equipped.Key, module));
                }

                using (var mySqlClient = SqlDatabaseManager.GetClient())
                    mySqlClient.ExecuteNonQuery($"UPDATE server_battlestations SET modules = '{JsonConvert.SerializeObject(modules)}' WHERE name = '{battleStation.AsteroidName}'");
            }
        }

        public static void LoadBattleStations()
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var data = (DataTable)mySqlClient.ExecuteQueryTable("SELECT * FROM server_battlestations");
                foreach (DataRow row in data.Rows)
                {
                    bool active = Convert.ToBoolean(row["active"]);

                    if (active)
                    {
                        string name = Convert.ToString(row["name"]);
                        int mapId = Convert.ToInt32(row["mapId"]);
                        int clanId = Convert.ToInt32(row["clanId"]);
                        int positionX = Convert.ToInt32(row["positionX"]);
                        int positionY = Convert.ToInt32(row["positionY"]);
                        var modules = JsonConvert.DeserializeObject<List<EquippedModuleBase>>(row["modules"].ToString());
                        var inBuildingState = Convert.ToBoolean(Convert.ToInt32(row["inBuildingState"]));
                        var buildTimeInMinutes = Convert.ToInt32(row["buildTimeInMinutes"]);
                        var buildTime = DateTime.Parse(row["buildTime"].ToString());
                        var deflectorActive = Convert.ToBoolean(Convert.ToInt32(row["deflectorActive"]));
                        var deflectorSecondsLeft = Convert.ToInt32(row["deflectorSecondsLeft"]);
                        var deflectorTime = DateTime.Parse(row["deflectorTime"].ToString());
                        var visualModifiers = JsonConvert.DeserializeObject<List<int>>(row["visualModifiers"].ToString());

                        var battleStation = new BattleStation(name, GameManager.GetSpacemap(mapId), new Position(positionX, positionY), GameManager.GetClan(clanId), modules, inBuildingState, buildTimeInMinutes, buildTime, deflectorActive, deflectorSecondsLeft, deflectorTime, visualModifiers);
                        GameManager.BattleStations.TryAdd(battleStation.Name, battleStation);
                    }
                }
            }
        }

        public static void LoadShips()
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var data = (DataTable)mySqlClient.ExecuteQueryTable("SELECT * FROM server_ships");
                foreach (DataRow row in data.Rows)
                {
                    string name = Convert.ToString(row["name"]);
                    int shipID = Convert.ToInt32(row["shipID"]);
                    int damage = Convert.ToInt32(row["damage"]);
                    int shields = Convert.ToInt32(row["shield"]);
                    int hitpoints = Convert.ToInt32(row["health"]);
                    int speed = Convert.ToInt32(row["speed"]);
                    string lootID = Convert.ToString(row["lootID"]);
                    int displayId = 0;
                    if (row.Table.Columns.Contains("ShipDisplayID") && row["ShipDisplayID"] != DBNull.Value && row["ShipDisplayID"].ToString() != "")
                        displayId = Convert.ToInt32(row["ShipDisplayID"]);
                    bool aggressive = Convert.ToBoolean(row["aggressive"]);
                    bool respawnable = Convert.ToBoolean(row["respawnable"]);
                    var rewards = JsonConvert.DeserializeObject<ShipRewards>(row["reward"].ToString());
                    var waves = JsonConvert.DeserializeObject<MinionWaves>(row["waves"].ToString());
                    int type = Convert.ToInt32(row["type"]);
                    var ores = JsonConvert.DeserializeObject<Cargo>(row["ores"].ToString());

                    var ship = new Ship(name, shipID, hitpoints, shields, speed, lootID, damage, aggressive, respawnable, rewards, waves, type, ores, displayId);
                    GameManager.Ships.TryAdd(ship.Id, ship);
                }
            }
        }

        public static void LoadClans()
        {
            GameManager.Clans.TryAdd(0, new Clan(0, "", "", 0));
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var data = (DataTable)mySqlClient.ExecuteQueryTable("SELECT * FROM server_clans");
                foreach (DataRow row in data.Rows)
                {
                    int id = Convert.ToInt32(row["id"]);
                    string name = Convert.ToString(row["name"]);
                    string tag = Convert.ToString(row["tag"]);
                    int factionId = Convert.ToInt32(row["factionId"]);

                    var clan = new Clan(id, name, tag, factionId);
                    GameManager.Clans.TryAdd(clan.Id, clan);
                    LoadClanDiplomacy(clan);
                }
            }
        }

        private static void LoadClanDiplomacy(Clan clan)
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var data = (DataTable)mySqlClient.ExecuteQueryTable($"SELECT * FROM server_clan_diplomacy WHERE senderClanId = {clan.Id}");
                foreach (DataRow row in data.Rows)
                {
                    int id = Convert.ToInt32(row["toClanId"]);
                    Diplomacy relation = (Diplomacy)Convert.ToInt32(row["diplomacyType"]);
                    clan.Diplomacies.Add(id, relation);
                }

                var data2 = (DataTable)mySqlClient.ExecuteQueryTable($"SELECT * FROM server_clan_diplomacy WHERE toClanId = {clan.Id}");
                foreach (DataRow row in data2.Rows)
                {
                    int id = Convert.ToInt32(row["senderClanId"]);
                    Diplomacy relation = (Diplomacy)Convert.ToInt32(row["diplomacyType"]);
                    clan.Diplomacies.Add(id, relation);
                }
            }
        }

        public static int GetChatPermission(int userId)
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var data = (DataTable)mySqlClient.ExecuteQueryTable($"SELECT * FROM chat_permissions WHERE userId = {userId}");
                foreach (DataRow row in data.Rows)
                {
                    return Convert.ToInt32(row["type"]);
                }
                return 0;
            }
        }
    }
}
