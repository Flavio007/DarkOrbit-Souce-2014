using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Diagnostics;

// Uses production objects and a map with no network sockets or background ticks.
class CarrierRegression
{
    static Assembly game;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type T(string name) { return game.GetType("Ow." + name, true); }
    static object Get(object obj, string name) { var p = obj.GetType().GetProperty(name, Flags); return p != null ? p.GetValue(obj, null) : obj.GetType().GetField(name, Flags).GetValue(obj); }
    static void Set(object obj, string name, object value) { var p = obj.GetType().GetProperty(name, Flags); if (p != null && p.CanWrite) p.SetValue(obj, value, null); else obj.GetType().GetField(name, Flags).SetValue(obj, value); }
    static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, Flags).Invoke(obj, args); }
    static object New(string name, params object[] args) { return Activator.CreateInstance(T(name), Flags, null, args, null); }
    static object Empty(string name)
    {
        var obj = FormatterServices.GetUninitializedObject(T(name));
        for (var type = obj.GetType(); type != null; type = type.BaseType)
            foreach (var field in type.GetFields(Flags | BindingFlags.DeclaredOnly))
                if (field.FieldType.IsGenericType && field.FieldType.GetConstructor(Type.EmptyTypes) != null &&
                    (field.FieldType.Namespace == "System.Collections.Concurrent" || field.FieldType.Namespace == "System.Collections.Generic"))
                    field.SetValue(obj, Activator.CreateInstance(field.FieldType));
        return obj;
    }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static void Step(object carrier, double seconds)
    {
        Set(carrier, "last", Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency - seconds);
        Call(carrier, "Tick");
    }
    static int Main(string[] args)
    {
        try
        {
            game = Assembly.LoadFrom(args[0]);
            var timeline = New("Game.Objects.CarrierTimeline");
            Call(timeline, "Begin", 2, 5.0);
            Call(timeline, "Advance", .6); Check(!(bool)Get(timeline, "Active"), "area damage waits for attackPrepare");
            Call(timeline, "Advance", .1); Check((bool)Get(timeline, "Active"), "area damage starts after preparation");
            Call(timeline, "Advance", 4.9); Check((bool)Get(timeline, "Active"), "aura remains active for five seconds");
            Call(timeline, "Advance", .1); Check(!(bool)Get(timeline, "Active") && (int)Get(timeline, "Mode") == 2, "reverse animation has no damage");
            Call(timeline, "Advance", 1.0); Check((int)Get(timeline, "Mode") == 0, "reverse animation returns to idle");
            Call(timeline, "Advance", 1e10); Check((double)Get(timeline, "Age") < 10, "idle does not accumulate uptime or overflow packet age");

            var manager = T("Managers.GameManager");
            var clans = manager.GetField("Clans").GetValue(null);
            Call(clans, "TryAdd", 0, New("Game.Clan", 0, "", "", 0, 0));
            var ships = manager.GetField("Ships").GetValue(null);
            if (args.Length > 1 && args[1] == "--database")
            {
                T("Managers.QueryManager").GetMethod("LoadShips").Invoke(null, null);
                foreach (var id in new[] { 99, 100, 102, 105, 1105 })
                {
                    var ship = manager.GetMethod("GetShip").Invoke(null, new object[] { id });
                    Check(ship != null && (int)Call(ship, "GetDisplayShipId") == id && (int)Get(ship, "BaseHitpoints") > 0,
                        "production database loader resolves NPC/display " + id);
                }
            }
            foreach (var id in new[] { 100, 1105 })
                Call(ships, "TryAdd", id, New("Game.Ship", "Test", id, 2000000, 1000000, 200, 0, id.ToString(), 1000, true, false,
                    New("Game.ShipRewards"), New("Game.MinionWaves"), 0, New("Game.Cargo"), id));
            var map = Empty("Game.Spacemap"); Set(map, "Id", 999);
            Set(map, "Options", New("Game.OptionsBase"));
            var limits = Array.CreateInstance(T("Game.Movements.Position"), 2);
            limits.SetValue(New("Game.Movements.Position", 0, 0), 0); limits.SetValue(New("Game.Movements.Position", 20000, 12000), 1); Set(map, "Limits", limits);
            var carrierShip = manager.GetMethod("GetShip").Invoke(null, new object[] { 1105 });
            var npc = New("Game.Objects.Npc", 40001, carrierShip, map, New("Game.Movements.Position", 10000, 6000), 0);
            var carrier = Get(npc, "Carrier");
            var target = Empty("Game.Objects.Player"); Set(target, "Spacemap", map);
            Step(carrier, .01);
            Check((int)Get(Get(carrier, "timeline"), "Mode") == 0, "peaceful carrier does not launch minions");
            Set(npc, "Selected", target); Set(npc, "Attacking", true);
            Step(carrier, 2.0);
            var minions = (IList)Get(carrier, "minions");
            Check(minions.Count == 0 && (int)Get(Get(carrier, "timeline"), "Mode") == 0,
                "aggro alone does not summon Infernal");
            Set(npc, "CurrentHitPoints", (int)Get(npc, "CurrentHitPoints") - 200000);
            Step(carrier, .01);
            Check((int)Get(Get(carrier, "timeline"), "Mode") == 1, "HP-loss threshold starts launch sequence");
            Step(carrier, 1.08);
            Check(minions.Count == 1, "first Infernal emerges after opening, not before");
            var position = Get(minions[0], "Position");
            Check((int)Get(position, "X") == 10000 && (int)Get(position, "Y") == 5760, "Infernal spawns at the upper world-space outlet");
            for (int i = 0; i < 5; i++) Step(carrier, .501);
            Check(minions.Count == 6, "wave emits six Infernal gradually");
            Check(Get(minions[0], "CarrierMother") == npc, "summoned Infernal retains its carrier escort link");
            var desired = (int)Get(carrier, "desiredMinions");
            Set(npc, "CurrentHitPoints", (int)Get(npc, "CurrentHitPoints") - 200000);
            Step(carrier, .01);
            Check((int)Get(carrier, "desiredMinions") == desired + 6, "HP-loss threshold requests another Infernal wave");
            for (int i = 0; i < 230; i++) Step(carrier, .51);
            Check(minions.Count == 12, "minutes of combat without further HP loss or deaths do not generate waves");
            Set(npc, "CurrentHitPoints", (int)Get(npc, "CurrentHitPoints") - 200000);
            for (int i = 0; i < 50; i++) Step(carrier, .51);
            Check(minions.Count == 18, "another HP threshold adds one wave and respects the live-minion cap");
            Set(minions[0], "Destroyed", true); Call(map, "RemoveCharacter", minions[0]);
            Step(carrier, .01);
            Check(minions.Count == 17 && (double)Get(carrier, "nextWave") <= (double)Get(carrier, "time") + 3.01,
                "dead Infernal schedules a replacement wave");
            Set(npc, "Selected", null); Set(npc, "Attacking", false);
            Call(Get(carrier, "timeline"), "Advance", 20.0);
            Set(carrier, "lastCombat", (double)Get(carrier, "time"));
            var hp = (int)Get(npc, "CurrentHitPoints");
            Step(carrier, 59.0); Check((int)Get(npc, "CurrentHitPoints") == hp, "carrier does not heal before one minute outside combat");
            Step(carrier, 1.1); Check((int)Get(npc, "CurrentHitPoints") > hp, "carrier heals after one minute outside combat");
            Call(npc, "Attack"); Check((int)Get(npc, "CurrentHitPoints") > hp, "carrier's normal attack path is disabled");
            var escort = minions[0]; var escortAI = Get(escort, "NpcAI");
            var outside = New("Game.Movements.Position", 11000, 6000);
            foreach (var field in new[] { "Position", "Destination", "OldPosition" }) Set(escort, field, outside);
            Set(escort, "Moving", false); Set(escort, "Attacking", false); Set(escort, "Selected", null);
            var escortNow = DateTime.Now;
            Set(escort, "LastCombatTime", escortNow.AddSeconds(-10)); Set(escort, "CarrierOrderTime", escortNow.AddSeconds(-10));
            Set(escortAI, "carrierEngaged", true);
            Call(escortAI, "TickCarrierEscort", escortNow);
            Check(!(bool)Get(escort, "Moving"), "escort waits until thirty seconds outside combat before returning");
            Call(escortAI, "TickCarrierEscort", escortNow.AddSeconds(21));
            var home = Get(escort, "Destination");
            Check((bool)Get(escort, "Moving") && (double)Call(home, "DistanceTo", Get(npc, "Position")) <= 400,
                "escort returns to an orbit around the current carrier position");
            int sectors = 0;
            var destinations = new object[minions.Count];
            for (int i = 0; i < minions.Count; i++) {
                var child = minions[i]; var ai = Get(child, "NpcAI");
                Set(child, "Attacking", false); Set(child, "Selected", null); Set(child, "Moving", false);
                Set(ai, "carrierOffsetReady", false); Set(ai, "carrierIdleUntil", DateTime.MinValue);
                Set(ai, "lastMovement", DateTime.MinValue);
                Call(ai, "TickCarrierEscort", escortNow.AddSeconds(22));
                var destination = Get(child, "Destination"); destinations[i] = destination;
                var sector = ((int)Get(destination, "X") >= 10000 ? 1 : 0) + ((int)Get(destination, "Y") >= 6000 ? 2 : 0);
                sectors |= 1 << sector;
            }
            Check(sectors == 15, "sequentially created escorts spread across all four sides of the carrier");
            Set(npc, "Position", New("Game.Movements.Position", 10120, 6070));
            bool follows = true;
            for (int i = 0; i < minions.Count; i++) {
                Call(Get(minions[i], "NpcAI"), "TickCarrierEscort", escortNow.AddSeconds(23));
                var destination = Get(minions[i], "Destination");
                follows &= (int)Get(destination, "X") - (int)Get(destinations[i], "X") == 120 &&
                    (int)Get(destination, "Y") - (int)Get(destinations[i], "Y") == 70;
            }
            Check(follows, "escort destinations follow the carrier translation without collapsing behind it");
            var saved = new object[minions.Count]; minions.CopyTo(saved, 0);
            Call(npc, "ClearCarrier");
            Check(minions.Count == 0, "carrier cleanup clears its wave");
            var ticks = T("Program").GetField("TickManager").GetValue(null);
            bool cleaned = true;
            foreach (var child in saved)
                cleaned &= (bool)Get(child, "Destroyed") && !(bool)Call(map, "ContainsCharacter", child) && !(bool)Call(ticks, "Exists", child);
            Check(cleaned, "all removed Infernal have no map presence or background ticks");
            Set(target, "Position", New("Game.Movements.Position", 4321, 5678));
            Set(target, "Storage", New("Game.Objects.Storage", target));
            Call(map, "AddCharacter", target);
            var spawn = T("Net.netty.handlers.LegacyModuleHandler").GetMethod("SpawnNpcs", BindingFlags.Static | BindingFlags.NonPublic);
            Func<string, string, string> command = (id, count) => (string)spawn.Invoke(null, new object[] { target, id, count });
            var characters = Get(map, "Characters");
            var before = (int)Get(characters, "Count");
            command("100", "3");
            Check((int)Get(characters, "Count") == before, "non-admin cannot spawn NPCs");
            Set(target, "RankId", 21);
            foreach (var bad in new[] { "0", "-1", "1001", "2147483648", "abc" }) command("100", bad);
            command("2147483647", "1");
            Check((int)Get(characters, "Count") == before, "invalid quantities and unknown IDs create nothing");
            Check(command("100", "3").StartsWith("Criados 3"), "admin spawn reports the requested count");
            command("1105", "2");
            Check((int)Get(characters, "Count") == before + 5, "spawn creates exactly the requested number");
            int infernal = 0, carriers = 0;
            foreach (var created in (IEnumerable)Get(characters, "Values"))
            {
                if (created == npc || created == target) continue;
                var point = Get(created, "Position");
                Check((int)Get(point, "X") == 4321 && (int)Get(point, "Y") == 5678, "spawned NPC uses the player's position");
                if ((int)Get(Get(created, "Ship"), "Id") == 100) infernal++;
                if (Get(created, "Carrier") != null) carriers++;
                Check(Get(created, "NpcAI") != null, "spawned NPC retains its AI");
            }
            Check(infernal == 3 && carriers == 2, "carrier ID keeps its special controller through admin spawn");
            if (args.Length > 1 && args[1] == "--database")
            {
                var factory = T("Game.Objects.Npc").GetMethod("CreateForShip", BindingFlags.Static | BindingFlags.NonPublic);
                foreach (var id in new[] { 80, 480, 880, 107 })
                {
                    var ship = manager.GetMethod("GetShip").Invoke(null, new object[] { id });
                    var special = factory.Invoke(null, new object[] { ship, map, New("Game.Movements.Position", 4321, 5678) });
                    Check(special.GetType().Name == (id == 107 ? "SolarLordakium" : "Cubikon"), "spawn factory preserves special AI for " + id);
                }
            }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
