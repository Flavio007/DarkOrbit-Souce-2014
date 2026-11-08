// Standalone regression harness. Uses reflection so the production assembly can
// keep its game objects internal and no database or listening sockets are needed.
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;

class NpcLifecycleRegression
{
    static Assembly game;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type T(string name) { return game.GetType(name, true); }
    static object Get(object value, string name)
    {
        var property = value.GetType().GetProperty(name, Flags);
        return property != null ? property.GetValue(value, null) : value.GetType().GetField(name, Flags).GetValue(value);
    }
    static void Set(object value, string name, object data)
    {
        var property = value.GetType().GetProperty(name, Flags);
        if (property != null && property.CanWrite) property.SetValue(value, data, null);
        else value.GetType().GetField(name, Flags).SetValue(value, data);
    }
    static object Call(object value, string name, params object[] args)
    {
        return value.GetType().GetMethod(name, Flags).Invoke(value, args);
    }
    static object Empty(string name)
    {
        var value = FormatterServices.GetUninitializedObject(T(name));
        for (var type = value.GetType(); type != null; type = type.BaseType)
            foreach (var field in type.GetFields(Flags | BindingFlags.DeclaredOnly))
                if (field.FieldType.IsGenericType && field.FieldType.GetConstructor(Type.EmptyTypes) != null
                    && (field.FieldType.Namespace == "System.Collections.Concurrent" || field.FieldType.Namespace == "System.Collections.Generic"))
                    field.SetValue(value, Activator.CreateInstance(field.FieldType));
        return value;
    }
    static object Position(int x, int y) { return Activator.CreateInstance(T("Ow.Game.Movements.Position"), x, y); }
    static object Map(int id)
    {
        var map = Empty("Ow.Game.Spacemap");
        Set(map, "Id", id);
        Set(map, "Options", Activator.CreateInstance(T("Ow.Game.OptionsBase")));
        var limits = Array.CreateInstance(T("Ow.Game.Movements.Position"), 2);
        limits.SetValue(Position(0, 0), 0);
        limits.SetValue(Position(20800, 12800), 1);
        Set(map, "Limits", limits);
        return map;
    }
    static object Character(string kind, int id, object map, int x = 0)
    {
        var value = Empty("Ow.Game.Objects." + kind);
        T("Ow.Game.Objects.Attackable").GetField("<Id>k__BackingField", Flags).SetValue(value, id);
        Set(value, "Spacemap", map);
        var position = Position(x, 0);
        foreach (var field in new[] { "Position", "Destination", "OldPosition", "Direction" }) Set(value, field, position);
        Set(value, "Storage", Activator.CreateInstance(T("Ow.Game.Objects.Storage"), Flags, null, new[] { value }, null));
        Set(value, "Ship", Empty("Ow.Game.Ship"));
        if (kind != "Player")
        {
            Set(value, "CurrentHitPoints", 100);
            Set(value, "MaxHitPoints", 100);
            Set(value, "MaxShieldPoints", 50);
            Set(value, "NpcAI", Activator.CreateInstance(T("Ow.Game.Objects.AI.NpcAI"), Flags, null, new[] { value }, null));
            Set(value, "AgroRange", 500);
        }
        Call(map, "AddCharacter", value);
        return value;
    }
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }
    static bool CanSee(object map, object a, object b) { return (bool)Call(map, "CanSeeCharacter", a, b); }
    static int Count(object dictionary) { return (int)Get(dictionary, "Count"); }
    static int Main(string[] args)
    {
        try
        {
            game = Assembly.LoadFrom(args[0]);
            var questOwner=Empty("Ow.Game.Objects.Player");
            var questSettings=Activator.CreateInstance(T("Ow.Game.Objects.Player").GetField("Settings",Flags).FieldType);
            Set(questOwner,"Settings",questSettings);
            var pilotTree=Activator.CreateInstance(T("Ow.Game.Objects.Player").GetField("SkillTree",Flags).FieldType);Set(questOwner,"SkillTree",pilotTree);
            Set(pilotTree,"shieldEngineering",4);Call(questOwner,"getShieldSkill");Check(!(bool)Get(questOwner,"shieldeng"),"shield bubble requires all five engineering points");
            Set(pilotTree,"shieldEngineering",5);Call(questOwner,"getShieldSkill");Check((bool)Get(questOwner,"shieldeng"),"five engineering points enable shield bubble");
            Set(pilotTree,"shieldEngineering",0);Call(questOwner,"getShieldSkill");Check(!(bool)Get(questOwner,"shieldeng"),"skill reset removes stale shield bubble flag");
            Set(pilotTree,"shieldMechanics",2);Set(questOwner,"CurrentShieldPoints",100);Check((bool)Call(questOwner,"ShieldMechanics"),"max mechanics and shield enable collision visuals");
            Set(questOwner,"CurrentShieldPoints",0);Check(!(bool)Call(questOwner,"ShieldMechanics"),"depleted shield disables collision visuals");
            var questFilters=Get(questSettings,"ClassY2T");
            Set(questFilters,"questsAvailableFilter",true);Set(questFilters,"questsUnavailableFilter",false);Set(questFilters,"questsCompletedFilter",false);
            var questRuntime=Empty("Ow.Game.Objects.Players.Quests");Set(questRuntime,"Player",questOwner);
            var questDefinition=Activator.CreateInstance(T("Ow.Game.Objects.Players.QuestDefinition"),true);
            var questState=Activator.CreateInstance(T("Ow.Game.Objects.Players.QuestPlayerState"),new object[]{101});
            Set(questState,"State",1);
            Check((bool)Call(questRuntime,"Include",questDefinition,questState),"accepted quests remain listed when unavailable filter is disabled");
            Set(questState,"State",2);
            Check(!(bool)Call(questRuntime,"Include",questDefinition,questState),"completed quests respect completed filter");
            Set(questFilters,"questsCompletedFilter",true);
            Check((bool)Call(questRuntime,"Include",questDefinition,questState),"completed filter restores completed quests");
            var poi = Activator.CreateInstance(T("Ow.Game.Objects.POI"), new object[]{"test", Enum.ToObject(T("Ow.Game.POITypes"),5), Enum.ToObject(T("Ow.Game.POIDesigns"),1), Enum.ToObject(T("Ow.Game.POIShapes"),2), new System.Collections.Generic.List<int>{100,0,110,0,110,100,100,100},true,false,""});
            Check((bool)Call(poi,"Contains",105d,50d) && !(bool)Call(poi,"Contains",90d,50d),"POI polygon geometry");
            double stop=(double)Call(poi,"MovementLimit",Position(0,50),Position(1000,50));
            Check(stop>0.09 && stop<0.1,"full movement segment stops before a thin wall despite legal endpoint");
            var verticalSlide=Call(poi,"SlideDestination",Position(90,20),Position(200,80));
            Check((int)Get(verticalSlide,"X")<100&&(int)Get(verticalSlide,"Y")==80,"slide along an internal vertical POE wall");
            var horizontalSlide=Call(poi,"SlideDestination",Position(102,-10),Position(108,80));
            Check((int)Get(horizontalSlide,"X")==108&&(int)Get(horizontalSlide,"Y")<0,"slide along an internal horizontal POE wall");
            Check((double)Call(poi,"MovementLimit",Position(105,50),Position(200,50))==1,"ship already inside a barrier can leave");
            Set(poi,"Active",false);Check(!(bool)Call(poi,"Blocks",1),"disabled POI does not block movement");Set(poi,"Active",true);
            Set(poi,"ShapeCoordinates",new System.Collections.Generic.List<int>{0,0,100,50});Set(poi,"ShapeId",(short)0);Set(poi,"Inverted",true);
            Check(!(bool)Call(poi,"Contains",0d,0d)&&(bool)Call(poi,"Contains",0d,60d),"inverted ellipse uses both radii");
            Check((double)Call(poi,"MovementLimit",Position(0,0),Position(0,200))<0.25,"inverted ellipse prevents leaving its safe interior");
            Check(!(bool)Call(poi,"Blocks",1),"circular POE does not block movement");
            Set(poi,"ShapeId",(short)1);Check(!(bool)Call(poi,"Blocks",1),"polygon POE does not block movement");
            Set(poi,"ShapeId",(short)2);Set(poi,"TypeId",(short)6);Set(poi,"TypeSpecification","EIC");
            Check((bool)Call(poi,"Blocks",2)&&!(bool)Call(poi,"Blocks",1),"faction restriction respects specification");
            Set(poi,"TypeId",(short)5);Set(poi,"ShapeId",(short)2);Set(poi,"Inverted",false);
            Set(poi,"ShapeCoordinates",new System.Collections.Generic.List<int>{100,-100,110,-100,110,100,100,100});
            var collisionMap=Map(99);Call(Get(collisionMap,"POIs"),"TryAdd","wall",poi);
            var movingNpc=Character("Npc",99,collisionMap);Set(Get(movingNpc,"Ship"),"BaseSpeed",300);
            T("Ow.Game.Movements.Movement").GetMethod("Move").Invoke(null,new object[]{movingNpc,Position(1000,0)});
            Check((int)Get(Get(movingNpc,"Destination"),"X")==1000,"NPC movement crosses POE walls freely");
            var radiationPlayer=Character("Player",100,collisionMap);
            Set(poi,"TypeId",(short)8);Set(poi,"Inverted",true);
            Call(collisionMap,"CheckRadiation",radiationPlayer);
            Check((bool)Get(Get(radiationPlayer,"Storage"),"IsInRadiationZone"),"radiation uses inverted polygon geometry");
            Set(poi,"Active",false);Call(collisionMap,"CheckRadiation",radiationPlayer);
            Check(!(bool)Get(Get(radiationPlayer,"Storage"),"IsInRadiationZone"),"inactive radiation has no effect");
            foreach(var mapId in new[]{20,24,28})
            {
                var serviceMap=Map(mapId);var faction=mapId==20?1:mapId==24?2:3;
                var home=Activator.CreateInstance(T("Ow.Game.Objects.Stations.HomeStation"),Flags,null,new object[]{serviceMap,faction,Position(10000,7000),Empty("Ow.Game.Clan")},null);
                var giver=Get(home,"QuestGiverStation");var trade=Get(home,"OreTradeStation");
                Check(giver!=null&&trade!=null&&(bool)Get(giver,"EmbeddedInHomeStation")&&(bool)Get(trade,"EmbeddedInHomeStation"),"x-8 creates embedded mission and ore trade services for faction "+faction);
                Check((int)Get(giver,"FactionId")==faction&&(int)Get(trade,"FactionId")==faction&&Count(Get(serviceMap,"Activatables"))==3,"x-8 services retain faction and register as real activatables");
                Check((int)Get(Get(giver,"Position"),"X")==9900&&(int)Get(Get(trade,"Position"),"X")==10100,"x-8 service bubbles have separated anchors around the existing hangar bubble");
                Call(home,"PrepareSolitaryServices");Check(Count(Get(serviceMap,"Activatables"))==3,"x-8 service initialization is idempotent");
                var missionPacket=(byte[])Call(giver,"GetAssetCreateCommand",(short)0);var tradePacket=(byte[])Call(trade,"GetAssetCreateCommand",(short)0);
                Check(System.Text.Encoding.UTF8.GetString(missionPacket).Contains("__home_service_quest")&&System.Text.Encoding.UTF8.GetString(tradePacket).Contains("__home_service_trade"),"x-8 service packets identify interaction anchors without extra hulls");
            }
            foreach (var homeId in new[] { 1, 5, 9 })
            {
                var home = Map(homeId);Set(home,"VisualMapId",homeId);var faction=(homeId-1)/4+1;
                var defender = Character("Player",1000,home,0);Set(defender,"FactionId",faction);
                var invader = Character("Player",1001,home,8000);Set(invader,"FactionId",faction%3+1);
                var friend = Character("Player",1002,home,10000);Set(friend,"FactionId",faction);
                var petNpc = Character("Npc",1003,home,100);Set(petNpc,"FactionId",faction%3+1);
                Check(CanSee(home,defender,invader),"home x-1 defender sees distant enemy faction "+faction);
                Check(!CanSee(home,invader,defender)&&!CanSee(home,defender,friend)&&!CanSee(home,petNpc,invader),"shared radar does not expand intruder/friendly/NPC range");
                Check((int)Call(home,"EnemyPlayerCount",defender)==1,"map-wide threat counts players only");
                Call(Get(defender,"InRangeCharacters"),"TryAdd",1001,invader);
                int recipients=0;foreach(var recipient in (IEnumerable)Call(invader,"PacketRecipients"))if(Object.ReferenceEquals(recipient,defender))++recipients;
                Check(recipients==1,"distant observer receives invader movement/effect packets");
                Call(Get(invader,"InRangeCharacters"),"TryAdd",1000,defender);recipients=0;
                foreach(var recipient in (IEnumerable)Call(invader,"PacketRecipients"))if(Object.ReferenceEquals(recipient,defender))++recipients;
                Check(recipients==1,"global and local packet recipients are deduplicated");
                Set(invader,"Destroyed",true);Check(!CanSee(home,defender,invader)&&(int)Call(home,"EnemyPlayerCount",defender)==0,"dead invader disappears from visibility and count");Set(invader,"Destroyed",false);
                Set(home,"VisualMapId",homeId+1);Check(!CanSee(home,defender,invader),"x-2 retains normal render range");
                var rule=T("Ow.Game.LowMapRadar");
                Check((bool)rule.GetMethod("Warning").Invoke(null,new object[]{homeId+1,faction,faction%3+1}),"x-2 sends the original warning icon flag");
                Check(!(bool)rule.GetMethod("Warning").Invoke(null,new object[]{homeId+2,faction,faction%3+1}),"x-3 does not send a special warning marker");
                Set(invader,"Spacemap",Map(99));Check((int)Call(home,"EnemyPlayerCount",defender)==0,"departed invader is removed from threat count");
            }
            var map = Map(1);
            var npc = Character("Npc", 1, map);
            var observer = Character("Npc", 2, map, 100);
            Check(CanSee(map, npc, observer), "nearby registered ships are visible");
            Set(npc, "Selected", observer);
            Set(observer, "Position", Position(5000, 0));
            Check(!CanSee(map, npc, observer), "target lock cannot bypass visibility distance");
            Set(observer, "AllMapRange", true);
            Check(CanSee(map, npc, observer) && CanSee(map, observer, npc), "map-wide visibility is symmetric");
            Set(observer, "Destroyed", true);
            Check(!CanSee(map, npc, observer), "dead ships are never visible");
            Set(observer, "Destroyed", false);
            Set(observer, "AllMapRange", false);
            Set(observer, "Position", Position(100, 0));
            Call(npc, "AddInRangeCharacter", observer);
            Set(observer, "Spacemap", Map(2));
            Check((bool)Call(npc, "RemoveInRangeCharacter", observer), "visibility cleanup survives map changes");
            Set(observer, "Spacemap", map);
            Call(npc, "AddInRangeCharacter", observer);
            Set(npc, "Selected", observer);
            Set(npc, "Attacking", true);
            Call(map, "RemoveCharacter", observer);
            Check(Get(npc, "Selected") == null && !(bool)Get(npc, "Attacking") && Count(Get(npc, "InRangeCharacters")) == 0,
                "removing a ship clears observer target, attack and visibility");
            var replacement = Character("Npc", 2, map, 100);
            Check(!(bool)Call(map, "RemoveCharacter", observer) && (bool)Call(map, "ContainsCharacter", replacement),
                "removing an old instance cannot delete a replacement with the same ID");
            Call(npc, "AddInRangeCharacter", replacement);
            Set(npc, "Selected", replacement);
            Set(npc, "Attacking", true);
            Set(replacement, "Selected", npc);
            Set(replacement, "Attacking", true);
            Set(replacement, "Destroyed", true);
            Call(replacement, "RespawnAt", Position(9000, 0));
            Check(Get(npc, "Selected") == null && !(bool)Get(npc, "Attacking"), "respawn clears the previous life's observers");
            Check(Get(replacement, "Selected") == null && !(bool)Get(replacement, "Attacking")
                && !(bool)Get(replacement, "Moving") && (int)Get(replacement, "CurrentHitPoints") == 100
                && Count(Get(replacement, "InRangeCharacters")) == 0 && (long)Get(replacement, "LifeVersion") == 1,
                "respawn publishes healthy stationary ship with clean combat state and new life");
            Set(npc, "Selected", replacement);
            Call(replacement, "BeginNewLife");
            Check(Get(npc, "Selected") == null, "selection cannot follow an object into a different life");
            var player = Character("Player", 3, map, 100);
            var invalid = Character("Player", 4, map, 50);
            Set(invalid, "Invisible", true);
            Call(npc, "AddInRangeCharacter", player);
            Call(npc, "AddInRangeCharacter", invalid);
            Set(npc, "Aggressive", true);
            var ai = Get(npc, "NpcAI");
            Call(ai, "AcquireTarget", false, true);
            Check(Object.ReferenceEquals(Get(npc, "Selected"), player) && (bool)Get(npc, "Attacking"),
                "aggressive NPC chooses valid player despite closer invisible candidate");
            Set(Get(invalid, "Storage"), "IsInDemilitarizedZone", true);
            Call(ai, "AcquireTarget", false, true);
            Check(Object.ReferenceEquals(Get(npc, "Selected"), player), "unrelated invalid player cannot clear valid target");
            Set(player, "UnderEmp", DateTime.Now.AddSeconds(5));
            Call(ai, "TickAI");
            Check(Get(npc, "Selected") == null && !(bool)Get(npc, "Attacking"), "EMP invalidates target before decision throttle");
            Set(player, "UnderEmp", DateTime.MinValue);
            Set(npc, "Aggressive", false);
            Call(ai, "AcquireTarget", false, true);
            Check(Get(npc, "Selected") == null, "passive NPC does not acquire peaceful players");
            Call(npc, "ReceiveAttack", player);
            Call(ai, "AcquireTarget", false, true);
            Check(Get(ai, "AIOption").ToString() == "FLY_TO_ENEMY", "passive NPC chases attacker when retaliating");
            var taunter = Character("Player", 300, map, 200);
            Set(ai, "nextDecisionTime", DateTime.Now.AddMinutes(1));
            Check((bool)Call(ai, "RetargetForDrawFire", taunter), "Draw Fire redirects an already attacking passive NPC");
            Check(Object.ReferenceEquals(Get(npc,"Selected"),taunter) && (bool)Get(npc,"Attacking") && Get(ai,"AIOption").ToString()=="FLY_TO_ENEMY",
                "Draw Fire keeps selection, attacking and chase state coherent");
            Check((DateTime)Get(ai,"nextDecisionTime")<=DateTime.Now, "Draw Fire schedules pursuit without the old decision delay");
            Set(invalid,"Invisible",true);
            Check(!(bool)Call(ai,"RetargetForDrawFire",invalid) && Object.ReferenceEquals(Get(npc,"Selected"),taunter) && (bool)Get(npc,"Attacking"),
                "rejected Draw Fire target preserves the existing attack");
            Set(ai,"AIOption",Enum.Parse(Get(ai,"AIOption").GetType(),"MINION"));
            Check((bool)Call(ai,"RetargetForDrawFire",taunter) && Get(ai,"AIOption").ToString()=="MINION", "Draw Fire preserves special minion AI roles");
            Set(ai,"AIOption",Enum.Parse(Get(ai,"AIOption").GetType(),"FLY_TO_ENEMY"));
            Set(npc,"Selected",player);
            Set(player, "Position", Position(6000, 0));
            Check(!(bool)Call(ai, "CanChasePlayer", player, false) && (bool)Call(ai, "CanChasePlayer", player, true),
                "normal chase is bounded while explicit event chase can span map");
            Call(ai, "TickAI");
            Check(Get(npc, "Selected") == null && !(bool)Get(npc, "Attacking"), "out-of-range target is released immediately");
            var deathMap = Map(10);
            var dying = Character("Npc", 10, deathMap);
            var watching = Character("Npc", 11, deathMap, 100);
            Call(watching, "AddInRangeCharacter", dying);
            Set(watching, "Selected", dying);
            Set(watching, "Attacking", true);
            // The synthetic NPC must have cargo and the real constructor's multiplier.
            var deathCargo = Empty("Ow.Game.Cargo");
            Set(deathCargo, "Prometium", 10);
            Set(Get(dying, "Ship"), "Drops", deathCargo);
            Set(dying, "CargoDropMultiplier", 1d);
            Set(dying, "Respawnable", true);
            Call(dying, "Destroy", null, Enum.Parse(T("Ow.Game.DestructionType"), "NPC"));
            Check(!(bool)Get(dying, "Destroyed") && (bool)Call(deathMap, "ContainsCharacter", dying)
                && (long)Get(dying, "LifeVersion") == 1 && (int)Get(dying, "CurrentHitPoints") == 100
                && Get(watching, "Selected") == null && !(bool)Get(watching, "Attacking")
                && Count(Get(watching, "InRangeCharacters")) == 0,
                "complete death pipeline clears old observers before automatic respawn");
            Check(Count(Get(deathMap, "Objects")) == 1, "one death creates one cargo box");
            var cube = Character("Cubikon", 12, deathMap);
            Set(cube, "Respawnable", true);
            Call(cube, "Destroy", null, Enum.Parse(T("Ow.Game.DestructionType"), "NPC"));
            Call(cube, "Tick");
            Check((bool)Get(cube, "Destroyed") && !(bool)Call(deathMap, "ContainsCharacter", cube)
                && (long)Get(cube, "LifeVersion") == 0 && (DateTime)Get(cube, "respawntime") > DateTime.Now,
                "Cubikon stays dead until scheduled respawn and cannot run combat AI");
            foreach (var kind in new[] { "Cubikon", "Protegit", "SolarLordakium" })
                foreach (var name in new[] { "NpcAI", "Attacking", "MotherShipId", "minioncount", "lastAttackTime", "lastShieldRepairTime" })
                    Check(T("Ow.Game.Objects." + kind).GetMember(name, Flags | BindingFlags.DeclaredOnly).Length == 0,
                        kind + " shares base " + name + " state");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

