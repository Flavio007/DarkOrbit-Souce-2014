using Newtonsoft.Json;
using Ow.Game.Objects;
using Ow.Managers;
using Ow.Managers.MySQLManager;
using Ow.Net.netty.commands;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects.Players.Managers
{
    class CpuManager : AbstractManager
    {
        private class CpuChargeEntry
        {
            public long InventoryItemId;
            public int ItemId;
            public string LootId;
            public int CurrentUses;
            public int MaxUses;
            public bool Dirty;

            public bool HasCharges => MaxUses > 0;
            public int RemainingUses => MaxUses <= 0 ? 0 : Math.Max(0, Math.Min(CurrentUses, MaxUses));
        }

        private readonly HashSet<string> equippedCpus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<long> equippedCpuInventoryItemIds = new HashSet<long>();
        private readonly Dictionary<string, int> ownedCpuCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<long, CpuChargeEntry> cpuInventoryEntries = new Dictionary<long, CpuChargeEntry>();
        private readonly Dictionary<string, List<CpuChargeEntry>> cpuEntriesByLoot = new Dictionary<string, List<CpuChargeEntry>>(StringComparer.OrdinalIgnoreCase);
        private bool enforceEquippedCpuValidation = false;

        // Cloaking CPU XS (CL04K-XS): cloaking unit with limited charge set (50).
        public const String CLOAK_XS_CPU = "equipment_extra_cpu_cl04k-xs";
        // Cloaking CPU MOD (CL04K-MOD): keeps cloaked until the next attack. {Do not apear in the hotbar}
        public const String CLOAK_MOD_CPU = "equipment_extra_cpu_cl04k-mod";
        // Cloaking CPU XL (CL04K-XL): high-capacity (50) cloaking CPU.
        public const String CLOAK_XL_CPU = "equipment_extra_cpu_cl04k-xl";
        // Backward-compatible alias used by the old logic.
        public const String CLK_XL = CLOAK_XL_CPU;

        // Auto Rocket CPU (AROL-X): auto-fires selected rockets while attacking.
        public const String AUTO_ROCKET_CPU = "equipment_extra_cpu_arol-x";
        // Rocket Launcher CPU (RLLB-X): controls automatic launcher behavior.
        public const String AUTO_HELLSTROM_CPU = "equipment_extra_cpu_rllb-x";
        // Rocket Launcher weapon (HST-2). {Bruh, why is this here, this is not a CPU}
        public const String ROCKET_LAUNCHER = "equipment_weapon_rocketlauncher_hst-2";
        // Jump CPU-2 (JP-02): standard jump CPU for map jumps.
        public const String GALAXY_JUMP_CPU = "equipment_extra_cpu_jp-02";

        // Targeting Guidance CPU-1: improves hit chance, lower tier.
        public const String AIM_01_CPU = "equipment_extra_cpu_aim-01";
        // Targeting Guidance CPU-2: improves hit chance, higher tier.
        public const String AIM_02_CPU = "equipment_extra_cpu_aim-02";
        // Advanced Jump CPU-1: advanced map-jump CPU.
        public const String AJP_01_CPU = "equipment_extra_cpu_ajp-01";
        // Auto Laser Boost CPU.
        public const String ALB_X_CPU = "equipment_extra_cpu_alb-x";
        // Anti-Z1 CPU. {Do not apear in the hotbar}
        public const String ANTI_Z1_CPU = "equipment_extra_cpu_anti-z1";
        // Anti-Z1 XL CPU.
        public const String ANTI_Z1_XL_CPU = "equipment_extra_cpu_anti-z1-xl";
        // Drone Repair CPU-1. {Do not apear in the hotbar}
        public const String DR_01_CPU = "equipment_extra_cpu_dr-01";
        // Drone Repair CPU-2. {Do not apear in the hotbar}
        public const String DR_02_CPU = "equipment_extra_cpu_dr-02";
        // Fuel Assistant CPU. {Do not apear in the hotbar}
        public const String FB_X_CPU = "equipment_extra_cpu_fb-x";
        // Insta-Shield CPU.
        public const String ISH_01_CPU = "equipment_extra_cpu_ish-01";
        // Smart Bomb CPU.
        public const String SMB_01_CPU = "equipment_extra_cpu_smb-01";
        // Jump CPU-1 10 charges can jump from any mapId < 12 back to x-1
        public const String JP_01_CPU = "equipment_extra_cpu_jp-01";
        // Jump CPU-2 20 charges can jump from any mapId < 29 back to x-1
        public const String JP_02_CPU = "equipment_extra_cpu_jp-02";
        // Turbo Mine CPU-1. {Do not apear in the hotbar}
        public const String MIN_T01_CPU = "equipment_extra_cpu_min-t01";
        // Turbo Mine CPU-2. {Do not apear in the hotbar}
        public const String MIN_T02_CPU = "equipment_extra_cpu_min-t02";
        // Engine Boost CPU.
        public const String NC_AGB_CPU = "equipment_extra_cpu_nc-agb";
        // Weapon Boost CPU.
        public const String NC_AWB_CPU = "equipment_extra_cpu_nc-awb";
        // Auto Laser Boost CPU.
        public const String NC_AWL_CPU = "equipment_extra_cpu_nc-awl";
        // Auto Rocket Boost CPU.
        public const String NC_AWR_CPU = "equipment_extra_cpu_nc-awr";
        // Auto Repair-Bot CPU.
        public const String NC_RRB_CPU = "equipment_extra_cpu_nc-rrb";
        // Cargo Compressor CPU.
        public const String GEMINEX_XI_CPU = "equipment_extra_cpu_geminex-xi";
        // Auto Particle Cannon CPU. {NOT USED ON THIS VERSION}
        public const String RGSL_CPU = "equipment_extra_cpu_rgsl";
        // Ammunition auto-buying CPU.
        public const String AM_CPU = "equipment_extra_cpu_am-cpu";
        // Rocket Auto-Buy CPU.
        public const String RB_X_CPU = "equipment_extra_cpu_rb-x";
        // Radar CPU. (Shows diplomacy status of nearby players on the minimap)
        public const String RD_X_CPU = "equipment_extra_cpu_rd-x";
        // Rocket Turbo CPU. {Do not apear in the hotbar}
        public const String ROK_T01_CPU = "equipment_extra_cpu_rok-t01";
        // Extra Slots CPU (+2). {Do not apear in the hotbar}
        public const String SLE_01_CPU = "equipment_extra_cpu_sle-01";
        // Extra Slots CPU (+4). {Do not apear in the hotbar}
        public const String SLE_02_CPU = "equipment_extra_cpu_sle-02";
        // Extra Slots CPU (+6). {Do not apear in the hotbar}
        public const String SLE_03_CPU = "equipment_extra_cpu_sle-03";
        // Extra Slots CPU (+10). {Do not apear in the hotbar}
        public const String SLE_04_CPU = "equipment_extra_cpu_sle-04";
        // HMD-07 extra module.
        public const String HMD_07 = "equipment_extra_hmd-07";
        // Repair Bot REP-1.
        public const String REPBOT_REP_1 = "equipment_extra_repbot_rep-1";
        // Repair Bot REP-2.
        public const String REPBOT_REP_2 = "equipment_extra_repbot_rep-2";
        // Repair Bot REP-3.
        public const String REPBOT_REP_3 = "equipment_extra_repbot_rep-3";
        // Repair Bot REP-4.
        public const String REPBOT_REP_4 = "equipment_extra_repbot_rep-4";
        // Repair Bot REP-S.
        public const String REPBOT_REP_S = "equipment_extra_repbot_rep-s";

        private const int AIM_XENOMIT_COST = 10;

        public CpuManager(Player player) : base(player) { }

        public DateTime cloakCooldown = new DateTime();

        public void SyncFromInventoryLoadout()
        {
            PersistCpuCharges();

            var selectedConfig = Player.CurrentConfig <= 0 ? 1 : Player.CurrentConfig;
            equippedCpus.Clear();
            equippedCpuInventoryItemIds.Clear();
            ownedCpuCounts.Clear();
            cpuInventoryEntries.Clear();
            cpuEntriesByLoot.Clear();
            enforceEquippedCpuValidation = false;

            try
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    LoadOwnedCpuData(mySqlClient);

                    var loadoutRows = mySqlClient.ExecuteQueryTable($"SELECT * FROM player_inventory_loadout WHERE userId = {Player.Id}") as DataTable;
                    if (loadoutRows == null)
                        return;

                    enforceEquippedCpuValidation = loadoutRows.Rows.Count > 0;
                    if (!enforceEquippedCpuValidation)
                        return;

                    foreach (DataRow row in loadoutRows.Rows)
                    {
                        var config = ParseInt(row, 1, "config_id", "configId", "config", "configuration");
                        if (config != selectedConfig)
                            continue;

                        var slotGroup = ReadRowString(row, "slot_group", "slotGroup", "group", "category");
                        if (!"cpus".Equals(slotGroup, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var mode = ReadRowString(row, "mode");
                        if (!string.IsNullOrWhiteSpace(mode) && !"ship".Equals(mode, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var lootId = ResolveCpuLootId(row);
                        if (!string.IsNullOrWhiteSpace(lootId))
                            equippedCpus.Add(lootId);

                        var inventoryItemId = ParseLong(row, 0L,
                            "inventory_item_id", "inventoryItemId", "item_instance_id", "itemInstanceId", "inventory_id", "inventoryId");
                        if (inventoryItemId > 0)
                            equippedCpuInventoryItemIds.Add(inventoryItemId);
                    }
                }
            }
            catch
            {
                enforceEquippedCpuValidation = false;
                equippedCpus.Clear();
                equippedCpuInventoryItemIds.Clear();
            }
        }

        public bool IsCpuEquipped(string cpu)
        {
            if (!enforceEquippedCpuValidation)
                return true;

            return equippedCpus.Contains(cpu);
        }

        public bool HasAnyCpuEquipped(params string[] cpus)
        {
            foreach (var cpu in cpus)
                if (!string.IsNullOrWhiteSpace(cpu) && IsCpuEquipped(cpu))
                    return true;

            return false;
        }

        public int GetCpuCount(string cpu, int legacyFallback = 0)
        {
            if (enforceEquippedCpuValidation && !IsCpuEquipped(cpu))
                return 0;

            List<CpuChargeEntry> entries;
            if (cpuEntriesByLoot.TryGetValue(cpu, out entries) && entries.Count > 0)
            {
                var filteredEntries = entries;
                if (enforceEquippedCpuValidation && equippedCpuInventoryItemIds.Count > 0)
                    filteredEntries = entries.Where(x => equippedCpuInventoryItemIds.Contains(x.InventoryItemId)).ToList();

                var chargedEntries = filteredEntries.Where(x => x.HasCharges).ToList();
                if (chargedEntries.Count > 0)
                {
                    var remainingUses = chargedEntries.Sum(x => x.RemainingUses);
                    return remainingUses;
                }

                return filteredEntries.Count;
            }

            int ownedCount;
            if (ownedCpuCounts.TryGetValue(cpu, out ownedCount) && ownedCount > 0)
                return ownedCount;

            return legacyFallback;
        }

        public bool HasCpuCharges(string cpu)
        {
            if (enforceEquippedCpuValidation && !IsCpuEquipped(cpu))
                return false;

            List<CpuChargeEntry> entries;
            if (!cpuEntriesByLoot.TryGetValue(cpu, out entries) || entries.Count == 0)
                return false;

            var filteredEntries = entries;
            if (enforceEquippedCpuValidation && equippedCpuInventoryItemIds.Count > 0)
                filteredEntries = entries.Where(x => equippedCpuInventoryItemIds.Contains(x.InventoryItemId)).ToList();

            return filteredEntries.Any(x => x.HasCharges);
        }

        public bool TryConsumeCpuCharge(string cpu)
        {
            if (enforceEquippedCpuValidation && !IsCpuEquipped(cpu))
                return false;

            List<CpuChargeEntry> entries;
            if (!cpuEntriesByLoot.TryGetValue(cpu, out entries) || entries.Count == 0)
                return true;

            var filteredEntries = entries;
            if (enforceEquippedCpuValidation && equippedCpuInventoryItemIds.Count > 0)
                filteredEntries = entries.Where(x => equippedCpuInventoryItemIds.Contains(x.InventoryItemId)).ToList();

            var chargedEntries = filteredEntries.Where(x => x.HasCharges).ToList();
            if (chargedEntries.Count == 0)
                return true;

            var entry = chargedEntries
                .Where(x => x.RemainingUses > 0)
                .OrderByDescending(x => x.RemainingUses)
                .FirstOrDefault();

            if (entry == null)
                return false;

            entry.CurrentUses -= 1;
            if (entry.CurrentUses < 0)
                entry.CurrentUses = 0;
            entry.Dirty = true;
            return true;
        }

        public void PersistCpuCharges()
        {
            if (cpuInventoryEntries.Count == 0)
                return;

            try
            {
                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    foreach (var entry in cpuInventoryEntries.Values)
                    {
                        if (entry == null || !entry.HasCharges || !entry.Dirty)
                            continue;

                        var currentUses = Math.Max(0, Math.Min(entry.CurrentUses, entry.MaxUses));
                        var updatedRows = mySqlClient.ExecuteNonQuery(
                            $"UPDATE player_inventory_cpu_charges SET current_uses = {currentUses}, max_uses = {entry.MaxUses}, updated_at = CURRENT_TIMESTAMP() WHERE inventory_item_id = {entry.InventoryItemId}"
                        );

                        if (updatedRows <= 0)
                        {
                            mySqlClient.ExecuteNonQuery(
                                $"INSERT INTO player_inventory_cpu_charges (userId, inventory_item_id, item_id, current_uses, max_uses) VALUES ({Player.Id}, {entry.InventoryItemId}, {entry.ItemId}, {currentUses}, {entry.MaxUses})"
                            );
                        }

                        entry.Dirty = false;
                    }
                }
            }
            catch
            {
            }
        }

        private void LoadOwnedCpuData(dynamic mySqlClient)
        {
            DataTable inventoryRows;
            try
            {
                inventoryRows = mySqlClient.ExecuteQueryTable(
                    $"SELECT pii.id AS inventory_item_id, pii.item_id, iid.legacy_count_key, iid.name FROM player_inventory_items pii LEFT JOIN inventory_item_definitions iid ON iid.item_id = pii.item_id WHERE pii.userId = {Player.Id}"
                ) as DataTable;
            }
            catch
            {
                return;
            }

            if (inventoryRows != null)
            {
                foreach (DataRow row in inventoryRows.Rows)
                {
                    var inventoryItemId = ParseLong(row, 0L, "inventory_item_id", "id");
                    var itemId = ParseInt(row, -1, "item_id", "itemId");
                    var legacyKey = ReadRowString(row, "legacy_count_key", "legacyCountKey");
                    var name = ReadRowString(row, "name", "title");
                    var lootId = ResolveCpuLootId(itemId, legacyKey, name);

                    if (string.IsNullOrWhiteSpace(lootId))
                        continue;

                    if (ownedCpuCounts.ContainsKey(lootId))
                        ownedCpuCounts[lootId] += 1;
                    else
                        ownedCpuCounts[lootId] = 1;

                    var entry = new CpuChargeEntry
                    {
                        InventoryItemId = inventoryItemId,
                        ItemId = itemId,
                        LootId = lootId,
                        CurrentUses = 0,
                        MaxUses = 0,
                        Dirty = false
                    };

                    cpuInventoryEntries[inventoryItemId] = entry;

                    List<CpuChargeEntry> entries;
                    if (!cpuEntriesByLoot.TryGetValue(lootId, out entries))
                    {
                        entries = new List<CpuChargeEntry>();
                        cpuEntriesByLoot[lootId] = entries;
                    }
                    entries.Add(entry);
                }
            }

            DataTable chargesRows;
            try
            {
                chargesRows = mySqlClient.ExecuteQueryTable(
                    $"SELECT inventory_item_id, item_id, current_uses, max_uses FROM player_inventory_cpu_charges WHERE userId = {Player.Id}"
                ) as DataTable;
            }
            catch
            {
                return;
            }

            if (chargesRows == null)
                return;

            foreach (DataRow row in chargesRows.Rows)
            {
                var inventoryItemId = ParseLong(row, 0L, "inventory_item_id", "inventoryItemId");
                var itemId = ParseInt(row, -1, "item_id", "itemId");
                var currentUses = Math.Max(0, ParseInt(row, 0, "current_uses", "currentUses"));
                var maxUses = Math.Max(0, ParseInt(row, 0, "max_uses", "maxUses"));

                CpuChargeEntry entry;
                if (!cpuInventoryEntries.TryGetValue(inventoryItemId, out entry))
                {
                    var lootId = ResolveCpuLootId(itemId, string.Empty, string.Empty);
                    if (string.IsNullOrWhiteSpace(lootId))
                        continue;

                    entry = new CpuChargeEntry
                    {
                        InventoryItemId = inventoryItemId,
                        ItemId = itemId,
                        LootId = lootId,
                        Dirty = false
                    };

                    cpuInventoryEntries[inventoryItemId] = entry;

                    List<CpuChargeEntry> entries;
                    if (!cpuEntriesByLoot.TryGetValue(lootId, out entries))
                    {
                        entries = new List<CpuChargeEntry>();
                        cpuEntriesByLoot[lootId] = entries;
                    }
                    entries.Add(entry);
                }

                entry.MaxUses = maxUses;
                entry.CurrentUses = Math.Min(currentUses, maxUses);
            }
        }

        private string ResolveCpuLootId(DataRow row)
        {
            var itemId = ParseInt(row, -1, "item_id", "itemId", "id");
            var legacyKey = ReadRowString(row, "legacy_count_key", "legacyCountKey");
            var merged = string.Join(" ",
                ReadRowString(row, "loot_id", "lootId", "item_loot_id", "itemLootId", "code"),
                ReadRowString(row, "name", "title"));

            return ResolveCpuLootId(itemId, legacyKey, merged);
        }

        private string ResolveCpuLootId(int itemId, string legacyKey, string merged)
        {
            switch (itemId)
                {
                case 137: return AUTO_ROCKET_CPU;
                case 138: return ALB_X_CPU;
                case 139: return CLOAK_XL_CPU;

                case 43:  return REPBOT_REP_S;
                case 44:  return REPBOT_REP_1;
                case 45:  return REPBOT_REP_2;
                case 46:  return REPBOT_REP_3;
                case 47:  return REPBOT_REP_4;

                case 48:  return GEMINEX_XI_CPU;
                case 49:  return JP_01_CPU;
                case 50:  return JP_02_CPU;
                case 51:  return AJP_01_CPU;
                case 52:  return SLE_01_CPU;
                case 53:  return SLE_02_CPU;
                case 54:  return SLE_03_CPU;
                case 55:  return SLE_04_CPU;
                case 56:  return ROK_T01_CPU;

                case 58:  return AUTO_HELLSTROM_CPU;
                case 59:  return RGSL_CPU;
                case 60:  return NC_AGB_CPU;
                case 61:  return CLOAK_MOD_CPU;
                case 62:  return CLOAK_XS_CPU;
                case 63:  return CLOAK_XL_CPU;
                case 64:  return AM_CPU;
                case 65:  return RB_X_CPU;
                case 66:  return FB_X_CPU;
                case 67:  return RD_X_CPU;
                case 68:  return ISH_01_CPU;
                case 69:  return AIM_01_CPU;
                case 70:  return AIM_02_CPU;
                case 71:  return SMB_01_CPU;
                case 72:  return MIN_T01_CPU;
                case 73:  return MIN_T02_CPU;
                case 74:  return NC_AWL_CPU;
                case 75:  return NC_AWR_CPU;
                case 76:  return NC_AWB_CPU;
                case 77:  return NC_RRB_CPU;
                case 78:  return DR_01_CPU;
                case 79:  return DR_02_CPU;
            }

            switch ((legacyKey ?? string.Empty).Trim())
            {
                case "arcpuCount":
                    return AUTO_ROCKET_CPU;
                case "arolxCount":
                    return AUTO_ROCKET_CPU;
                case "clkcpuCount":
                    return CLOAK_XL_CPU;
                case "clo4kXlCpuCount":
                    return CLOAK_XL_CPU;
                case "clo4kCpuCount":
                    return CLOAK_XS_CPU;
                case "arlcpuCount":
                    return ALB_X_CPU;
                case "rllb1CpuCount":
                    return AUTO_HELLSTROM_CPU;
                case "jp01CpuCount":
                    return JP_01_CPU;
                case "jp02CpuCount":
                    return JP_02_CPU;
                case "ajp01CpuCount":
                    return AJP_01_CPU;
                case "sl01CpuCount":
                    return SLE_01_CPU;
                case "sl02CpuCount":
                    return SLE_02_CPU;
                case "sl03CpuCount":
                    return SLE_03_CPU;
                case "sl04CpuCount":
                    return SLE_04_CPU;
                case "r0kT01Count":
                    return ROK_T01_CPU;
                case "ncAgbCpuCount":
                    return NC_AGB_CPU;
                case "amCpuCount":
                    return AM_CPU;
                case "rbCpuCount":
                    return RB_X_CPU;
                case "fbXCpuCount":
                    return FB_X_CPU;
                case "rdCpuCount":
                    return RD_X_CPU;
                case "ish01CpuCount":
                    return ISH_01_CPU;
                case "aim01CpuCount":
                    return AIM_01_CPU;
                case "aim02CpuCount":
                    return AIM_02_CPU;
                case "smb01CpuCount":
                    return SMB_01_CPU;
                case "mint01CpuCount":
                    return MIN_T01_CPU;
                case "mint02CpuCount":
                    return MIN_T02_CPU;
                case "ncAwlCount":
                    return NC_AWL_CPU;
                case "ncAwrCount":
                    return NC_AWR_CPU;
                case "ncAwbCount":
                    return NC_AWB_CPU;
                case "ncRrbCpuCount":
                    return NC_RRB_CPU;
                case "dr01CpuCount":
                    return DR_01_CPU;
                case "dr02CpuCount":
                    return DR_02_CPU;
                case "geminexCount":
                    return GEMINEX_XI_CPU;
                case "rgslCount":
                    return RGSL_CPU;
                case "repSCount":
                    return REPBOT_REP_S;
                case "rep1Count":
                    return REPBOT_REP_1;
                case "rep2Count":
                    return REPBOT_REP_2;
                case "rep3Count":
                    return REPBOT_REP_3;
                case "rep4Count":
                    return REPBOT_REP_4;
            }

            var normalized = Normalize(merged);

            if (normalized.Contains("equipmentextracpuarolx") || normalized.Contains("arolx"))
                return AUTO_ROCKET_CPU;
            if (normalized.Contains("equipmentextracpurllbx") || normalized.Contains("equipmentextracpurllb1") || normalized.Contains("rllbx") || normalized.Contains("rllb1"))
                return AUTO_HELLSTROM_CPU;
            if (normalized.Contains("equipmentextracpucl04kxl") || normalized.Contains("cl04kxl"))
                return CLOAK_XL_CPU;
            if (normalized.Contains("equipmentextracpucl04kxs") || normalized.Contains("cl04kxs") || normalized.Contains("clo4kxscpu"))
                return CLOAK_XS_CPU;
            if (normalized.Contains("equipmentextracpualbx") || normalized.Contains("albx"))
                return ALB_X_CPU;
            if (normalized.Contains("equipmentextracpuish01") || normalized.Contains("ish01"))
                return ISH_01_CPU;
            if (normalized.Contains("equipmentextracpusmb01") || normalized.Contains("smb01"))
                return SMB_01_CPU;
            if (normalized.Contains("equipmentextracpujp01") || normalized.Contains("jp01"))
                return JP_01_CPU;
            if (normalized.Contains("equipmentextracpujp02") || normalized.Contains("jp02"))
                return JP_02_CPU;
            if (normalized.Contains("equipmentextracpuajp01") || normalized.Contains("ajp01"))
                return AJP_01_CPU;
            if (normalized.Contains("equipmentextracpuaim01") || normalized.Contains("aim01"))
                return AIM_01_CPU;
            if (normalized.Contains("equipmentextracpuaim02") || normalized.Contains("aim02"))
                return AIM_02_CPU;
            if (normalized.Contains("equipmentextracpuncagb") || normalized.Contains("ncagb"))
                return NC_AGB_CPU;
            if (normalized.Contains("equipmentextracpuncawl") || normalized.Contains("ncawl"))
                return NC_AWL_CPU;
            if (normalized.Contains("equipmentextracpuncawr") || normalized.Contains("ncawr"))
                return NC_AWR_CPU;
            if (normalized.Contains("equipmentextracpuncawb") || normalized.Contains("ncawb"))
                return NC_AWB_CPU;
            if (normalized.Contains("equipmentextracpuncrrb") || normalized.Contains("ncrrb"))
                return NC_RRB_CPU;
            if (normalized.Contains("equipmentextracpuamcpu") || normalized.Contains("amcpu"))
                return AM_CPU;
            if (normalized.Contains("equipmentextracpurbx") || normalized.Contains("equipmentextracpurbcpu") || normalized.Contains("rbx") || normalized.Contains("rbcpu"))
                return RB_X_CPU;
            if (normalized.Contains("equipmentextracpufbx") || normalized.Contains("fbx"))
                return FB_X_CPU;
            if (normalized.Contains("equipmentextracpurdx") || normalized.Contains("equipmentextracpurdcpu") || normalized.Contains("rdx") || normalized.Contains("rdcpu"))
                return RD_X_CPU;
            if (normalized.Contains("equipmentextracpudr01") || normalized.Contains("dr01"))
                return DR_01_CPU;
            if (normalized.Contains("equipmentextracpudr02") || normalized.Contains("dr02"))
                return DR_02_CPU;
            if (normalized.Contains("equipmentextracpurokt01") || normalized.Contains("rokt01"))
                return ROK_T01_CPU;
            if (normalized.Contains("equipmentextracpugeminexxi") || normalized.Contains("geminex"))
                return GEMINEX_XI_CPU;
            if (normalized.Contains("equipmentextracpumint01") || normalized.Contains("mint01"))
                return MIN_T01_CPU;
            if (normalized.Contains("equipmentextracpumint02") || normalized.Contains("mint02"))
                return MIN_T02_CPU;
            if (normalized.Contains("equipmentextracpucl04kmod") || normalized.Contains("cl04kmod"))
                return CLOAK_MOD_CPU;
            if (normalized.Contains("equipmentextrarepbotreps") || normalized.Contains("reps"))
                return REPBOT_REP_S;
            if (normalized.Contains("equipmentextrarepbotrep1") || normalized.Contains("rep1"))
                return REPBOT_REP_1;
            if (normalized.Contains("equipmentextrarepbotrep2") || normalized.Contains("rep2"))
                return REPBOT_REP_2;
            if (normalized.Contains("equipmentextrarepbotrep3") || normalized.Contains("rep3"))
                return REPBOT_REP_3;
            if (normalized.Contains("equipmentextrarepbotrep4") || normalized.Contains("rep4"))
                return REPBOT_REP_4;
            if (normalized.Contains("equipmentextracpurgsl") || normalized.Contains("rgsl"))
                return RGSL_CPU;

            return null;
        }

        private static long ParseLong(DataRow row, long defaultValue, params string[] candidateColumns)
        {
            foreach (var column in candidateColumns)
            {
                if (!row.Table.Columns.Contains(column))
                    continue;

                try
                {
                    var raw = row[column];
                    if (raw == null || raw == DBNull.Value)
                        continue;

                    long parsed;
                    if (long.TryParse(Convert.ToString(raw), out parsed))
                        return parsed;
                }
                catch
                {
                }
            }

            return defaultValue;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var chars = value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
            return new string(chars);
        }

        private static int ParseInt(DataRow row, int defaultValue, params string[] candidateColumns)
        {
            foreach (var column in candidateColumns)
            {
                if (!row.Table.Columns.Contains(column))
                    continue;

                try
                {
                    var raw = row[column];
                    if (raw == null || raw == DBNull.Value)
                        continue;

                    int parsed;
                    if (int.TryParse(Convert.ToString(raw), out parsed))
                        return parsed;
                }
                catch
                {
                }
            }

            return defaultValue;
        }

        private static string ReadRowString(DataRow row, params string[] candidateColumns)
        {
            foreach (var column in candidateColumns)
            {
                if (!row.Table.Columns.Contains(column))
                    continue;

                try
                {
                    var raw = row[column];
                    if (raw == null || raw == DBNull.Value)
                        continue;

                    var str = Convert.ToString(raw);
                    if (!string.IsNullOrWhiteSpace(str))
                        return str;
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private bool EnsureCpuEquipped(string cpu, string displayName)
        {
            if (IsCpuEquipped(cpu))
                return true;

            RemoveSelectedCpu(cpu);
            Player.SendPacket($"0|A|STM|{displayName} not equipped in current configuration.");
            Player.SettingsManager.SendNewItemStatus(cpu);
            return false;
        }

        private string GetCpuDisplayName(string cpu)
        {
            switch (cpu)
            {
                case REPBOT_REP_S: return "REP-S";
                case REPBOT_REP_1: return "REP-1";
                case REPBOT_REP_2: return "REP-2";
                case REPBOT_REP_3: return "REP-3";
                case REPBOT_REP_4: return "REP-4";
                case AIM_01_CPU: return "AIM-01";
                case AIM_02_CPU: return "AIM-02";
                case JP_01_CPU: return "JP-01";
                case JP_02_CPU: return "JP-02";
                case AJP_01_CPU: return "AJP-01";
                default: return cpu;
            }
        }

        public bool IsRepairBotCpu(string cpu)
        {
            switch (cpu)
            {
                case REPBOT_REP_S:
                case REPBOT_REP_1:
                case REPBOT_REP_2:
                case REPBOT_REP_3:
                case REPBOT_REP_4:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsAimCpu(string cpu)
        {
            return cpu == AIM_01_CPU || cpu == AIM_02_CPU;
        }

        public bool HasActiveRepairBot()
        {
            return IsCpuEquipped(REPBOT_REP_S) || IsCpuEquipped(REPBOT_REP_1) || IsCpuEquipped(REPBOT_REP_2) ||
                   IsCpuEquipped(REPBOT_REP_3) || IsCpuEquipped(REPBOT_REP_4);
        }

        public bool CanUseRepairBot()
        {
            return HasActiveRepairBot() || IsCpuEquipped(NC_RRB_CPU);
        }

        public int GetSelectedRepairBotId()
        {
            if (Player.Settings.InGameSettings.selectedCpus.Contains(REPBOT_REP_4))
                return 4;
            if (Player.Settings.InGameSettings.selectedCpus.Contains(REPBOT_REP_3))
                return 3;
            if (Player.Settings.InGameSettings.selectedCpus.Contains(REPBOT_REP_2))
                return 2;
            if (Player.Settings.InGameSettings.selectedCpus.Contains(REPBOT_REP_1))
                return 1;
            if (Player.Settings.InGameSettings.selectedCpus.Contains(REPBOT_REP_S))
                return 5;

            if (IsCpuEquipped(REPBOT_REP_4))
                return 4;
            if (IsCpuEquipped(REPBOT_REP_3))
                return 3;
            if (IsCpuEquipped(REPBOT_REP_2))
                return 2;
            if (IsCpuEquipped(REPBOT_REP_1))
                return 1;
            if (IsCpuEquipped(REPBOT_REP_S))
                return 5;

            return 0;
        }

        public void SyncSelectedCpus()
        {
            var selected = Player.Settings.InGameSettings.selectedCpus.ToList();
            foreach (var cpu in selected)
            {
                if (!IsCpuEquipped(cpu) || IsRepairBotCpu(cpu))
                    Player.Settings.InGameSettings.selectedCpus.Remove(cpu);
            }

            Player.Storage.AutoRocket = Player.Settings.InGameSettings.selectedCpus.Contains(AUTO_ROCKET_CPU) && IsCpuEquipped(AUTO_ROCKET_CPU);
            Player.Storage.AutoRocketLauncher = Player.Settings.InGameSettings.selectedCpus.Contains(AUTO_HELLSTROM_CPU) && IsCpuEquipped(AUTO_HELLSTROM_CPU);
            Player.AttackManager.RocketLauncher.ReloadingActive = Player.Storage.AutoRocketLauncher;

            Player.Invisible = Player.Settings.InGameSettings.selectedCpus.Contains(CLK_XL) && IsCpuEquipped(CLK_XL);

            var repairBotId = GetSelectedRepairBotId();
            Player.RepairBotId = repairBotId > 0 ? (byte)repairBotId : (byte)0;

            if (repairBotId <= 0 && Player.Storage.RepairBotActivated)
                Player.RepairBot(false);
        }

        public int GetCargoCapacityBonus(int baseCargo)
        {
            if (baseCargo <= 0 || !IsCpuEquipped(GEMINEX_XI_CPU))
                return 0;

            return baseCargo;
        }

        public int GetAimCpuMissReductionPercent()
        {
            if (IsCpuEquipped(AIM_02_CPU))
                return 50;
            if (IsCpuEquipped(AIM_01_CPU))
                return 25;

            return 0;
        }

        public void ConsumeAimCpuXenomit()
        {
            if (GetAimCpuMissReductionPercent() <= 0 || Player.Xenomit < AIM_XENOMIT_COST)
                return;

            // Aim CPU consumption is not a collection event; avoid sending the full ore cargo on every shot.
            Player.ChangeCargo(Ow.Game.Ores.Xenomit, -AIM_XENOMIT_COST, false, true, false);
        }

        public double GetRocketCooldownMultiplier()
        {
            return IsCpuEquipped(ROK_T01_CPU) ? 0.5 : 1.0;
        }

        public double GetMineCooldownMultiplier()
        {
            if (IsCpuEquipped(MIN_T02_CPU))
                return 0.5;
            if (IsCpuEquipped(MIN_T01_CPU))
                return 0.75;

            return 1.0;
        }

        public void UseRepairBot(string cpu)
        {
            if (!IsRepairBotCpu(cpu))
                return;

            if (!EnsureCpuEquipped(cpu, GetCpuDisplayName(cpu)))
                return;

            switch (cpu)
            {
                case REPBOT_REP_1:
                    Player.RepairBotId = 1;
                    break;
                case REPBOT_REP_2:
                    Player.RepairBotId = 2;
                    break;
                case REPBOT_REP_3:
                    Player.RepairBotId = 3;
                    break;
                case REPBOT_REP_4:
                    Player.RepairBotId = 4;
                    break;
                case REPBOT_REP_S:
                    Player.RepairBotId = 5;
                    break;
            }

            if (Player.CurrentHitPoints < Player.MaxHitPoints && !Player.AttackingOrUnderAttack() && !Player.Moving)
                Player.RepairBot(true);
            else if (Player.Storage.RepairBotActivated)
                Player.RepairBot(false);

            Player.SettingsManager.SendNewItemStatus(REPBOT_REP_S);
            Player.SettingsManager.SendNewItemStatus(REPBOT_REP_1);
            Player.SettingsManager.SendNewItemStatus(REPBOT_REP_2);
            Player.SettingsManager.SendNewItemStatus(REPBOT_REP_3);
            Player.SettingsManager.SendNewItemStatus(REPBOT_REP_4);
        }

        public void ToggleAimCpu(string cpu)
        {
            if (!IsAimCpu(cpu))
                return;

            if (!EnsureCpuEquipped(cpu, GetCpuDisplayName(cpu)))
                return;

            if (Player.Settings.InGameSettings.selectedCpus.Contains(cpu))
                RemoveSelectedCpu(cpu);
            else
                AddSelectedCpu(cpu);

            Player.SettingsManager.SendNewItemStatus(cpu);
        }

        public void JumpCpu(string cpu)
        {
            var displayName = GetCpuDisplayName(cpu);
            if (!EnsureCpuEquipped(cpu, displayName))
                return;

            if (Player.Storage.Jumping || Player.Spacemap == null)
                return;

            var mapId = Player.Spacemap.Id;
            var maxMapId = cpu == JP_01_CPU ? 11 : 28;

            if (mapId < 1 || mapId > maxMapId)
            {
                Player.SendPacket($"0|A|STM|{displayName} cannot be used on this map.");
                return;
            }

            if (!TryConsumeCpuCharge(cpu))
            {
                Player.SendPacket($"0|A|STM|{displayName} has no jumps remaining.");
                Player.SettingsManager.SendNewItemStatus(cpu);
                return;
            }

            Player.SettingsManager.SendNewItemStatus(cpu);
            Player.Jump(Player.GetBaseMapId(), Player.GetBasePosition());
        }

        public void AdvancedJumpCpu()
        {
            if (!EnsureCpuEquipped(AJP_01_CPU, "AJP-01"))
                return;

            Player.SendPacket("0|A|STM|AJP-01 requires the advanced jump starmap flow, which is not wired on this server yet.");
        }

        public void Cloak()
        {
            if (!EnsureCpuEquipped(CLOAK_XL_CPU, "CL04K-XL"))
                return;

            if (Player.Storage.Skills.TryGetValue(SkillManager.SPEARHEAD_ULTIMATE_CLOAK, out var ultimateCloakSkill) && ultimateCloakSkill.Active)
                ultimateCloakSkill.Disable();

            if (Player.Spacemap.Options.CloakBlocked || Player.Invisible) return;
            var consumedCharge = TryConsumeCpuCharge(CLOAK_XL_CPU);

            if (!consumedCharge)
            {
                Player.SendPacket("0|A|STM|CL04K-XL has no charges remaining.");
                Player.SettingsManager.SendNewItemStatus(CLK_XL);
                return;
            }

            EnableCloak();
            Player.SettingsManager.SendNewItemStatus(CLK_XL);
        }

        public void ArolX()
        {
            if(!Player.Storage.AutoRocket)
                EnableArolX();
            else
                DisableArolX();
        }

        public void RllbX()
        {
            if (!Player.Storage.AutoRocketLauncher)
                EnableRllbX();
            else
                DisableRllbX();
        }

        public void EnableCloak()
        {
            if (!Player.Invisible)
            {
                AddSelectedCpu(CLK_XL);
                Player.Invisible = true;
                string cloakPacket = "0|n|INV|" + Player.Id + "|1";
                Player.SendPacket(cloakPacket);
                Player.SendPacketToInRangePlayers(cloakPacket);

                if (Player.Pet != null && Player.Pet.Activated)
                {
                    Player.Pet.Invisible = true;
                    Player.Pet.SendPacketToInRangePlayers("0|n|INV|" + Player.Pet.Id + "|1");
                }

                Player.SettingsManager.SendNewItemStatus(CLK_XL);
            }
        }

        public void DisableCloak()
        {
            if (Player.Invisible)
            {
                RemoveSelectedCpu(CLK_XL);
                Player.Invisible = false;
                string cloakPacket = "0|n|INV|" + Player.Id + "|0";
                Player.SendPacket("0|A|STM|msg_uncloaked");
                Player.SendPacket(cloakPacket);
                Player.SendPacketToInRangePlayers(cloakPacket);
                Player.SettingsManager.SendNewItemStatus(CLK_XL);
            }

            if (Player.Pet != null && Player.Pet.Activated && Player.Pet.Invisible)
            {
                Player.Pet.Invisible = false;
                Player.Pet.SendPacketToInRangePlayers("0|n|INV|" + Player.Pet.Id + "|0");
            }
        }

        public void EnableArolX()
        {
            if (!EnsureCpuEquipped(AUTO_ROCKET_CPU, "AROL-X"))
                return;

            AddSelectedCpu(AUTO_ROCKET_CPU);
            Player.Storage.AutoRocket = true;
            Player.SettingsManager.SendNewItemStatus(AUTO_ROCKET_CPU);
        }

        public void DisableArolX()
        {
            RemoveSelectedCpu(AUTO_ROCKET_CPU);
            Player.Storage.AutoRocket = false;
            Player.SettingsManager.SendNewItemStatus(AUTO_ROCKET_CPU);
        }

        public void EnableRllbX()
        {
            if (!EnsureCpuEquipped(AUTO_HELLSTROM_CPU, "RLLB-X"))
                return;

            AddSelectedCpu(AUTO_HELLSTROM_CPU);
            Player.Storage.AutoRocketLauncher = true;
            Player.AttackManager.RocketLauncher.ReloadingActive = true;
            Player.SettingsManager.SendNewItemStatus(AUTO_HELLSTROM_CPU);
        }

        public void DisableRllbX()
        {
            RemoveSelectedCpu(AUTO_HELLSTROM_CPU);
            Player.Storage.AutoRocketLauncher = false;
            Player.AttackManager.RocketLauncher.ReloadingActive = false;
            Player.SettingsManager.SendNewItemStatus(AUTO_HELLSTROM_CPU);
        }

        public void AddSelectedCpu(string cpu)
        {
            if (!Player.Settings.InGameSettings.selectedCpus.Contains(cpu))
                Player.Settings.InGameSettings.selectedCpus.Add(cpu);
        }

        public void RemoveSelectedCpu(string cpu)
        {
            if (Player.Settings.InGameSettings.selectedCpus.Contains(cpu))
                Player.Settings.InGameSettings.selectedCpus.Remove(cpu);
        }
    }
}
