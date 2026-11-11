using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
class TutorialVideoRegression
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o,null):o.GetType().GetField(n,F).GetValue(o);}
    static void Set(object o,string n,object v){var p=o.GetType().GetProperty(n,F);if(p!=null)p.SetValue(o,v,null);else o.GetType().GetField(n,F).SetValue(o,v);}
    static object Empty(Type t){return FormatterServices.GetUninitializedObject(t);}
    static void Check(bool b,string m){if(!b)throw new Exception(m);Console.WriteLine("PASS "+m);}
    static int Main(string[] args){try{
        var a=Assembly.LoadFrom(args[0]);var managerType=a.GetType("Ow.Game.Events.TutorialManager",true);
        var manager=Activator.CreateInstance(managerType,true);var instanceType=managerType.GetNestedType("TutorialInstance",BindingFlags.NonPublic);
        var instance=Activator.CreateInstance(instanceType,true);var map=Empty(a.GetType("Ow.Game.Spacemap"));Set(map,"Id",20123);
        var player=Empty(a.GetType("Ow.Game.Objects.Player"));Set(player,"Spacemap",map);
        // Player Id is declared on Attackable and read-only.
        a.GetType("Ow.Game.Objects.Attackable").GetField("<Id>k__BackingField",F).SetValue(player,77);
        Set(instance,"OwnerId",77);Set(instance,"Spacemap",map);Set(instance,"MapId",20123);
        ((IDictionary)Get(manager,"instancesByMap")).Add(20123,instance);
        var npcType=a.GetType("Ow.Game.Objects.Npc");var cargoType=a.GetType("Ow.Game.Objects.Collectables.CargoBox");
        var npcs=(IList)Get(instance,"Streuners");var cargos=new object[3];
        for(int i=0;i<3;i++){var npc=Empty(npcType);Set(npc,"Spacemap",map);a.GetType("Ow.Game.Objects.Attackable").GetField("<Id>k__BackingField",F).SetValue(npc,100+i);npcs.Add(npc);}
        var rockets=managerType.GetMethod("ShouldTeachRockets",BindingFlags.Static|BindingFlags.NonPublic);
        Check(!(bool)rockets.Invoke(null,new[]{instance}),"first Streuner uses laser lesson");
        var destroyed=managerType.GetMethod("HandleNpcDestroyed",F);var collected=managerType.GetMethod("HandleCargoCollected",F);
        for(int i=0;i<3;i++){var npc=npcs[0];var cargo=Empty(cargoType);Set(cargo,"Spacemap",map);Set(cargo,"Hash","tutorial-"+i);cargos[i]=cargo;destroyed.Invoke(manager,new[]{npc,player,cargo});Check((bool)Get(cargo,"TutorialCargo"),"cargo does not expire during tutorial");Check((bool)rockets.Invoke(null,new[]{instance})==(i==0),"rocket lesson after first kill, collection retained for later");if(i==0){collected.Invoke(manager,new[]{cargo,player});Check((bool)rockets.Invoke(null,new[]{instance}),"collecting first cargo early preserves second Streuner rocket hint");}}
        Check(!(bool)Get(instance,"RewardsSpawned")&&((IDictionary)Get(instance,"Cargo")).Count==2 && (int)Get(instance,"CollectedCargo")==1,"three kills do not skip cargo collection");
        collected.Invoke(manager,new[]{cargos[0],player});collected.Invoke(manager,new[]{cargos[0],player});
        Check((int)Get(instance,"CollectedCargo")==1,"duplicate collection cannot advance tutorial twice");
        collected.Invoke(manager,new[]{cargos[1],player});
        Check(!(bool)Get(instance,"RewardsSpawned")&&(int)Get(instance,"CollectedCargo")==2,"exit remains locked before third cargo");
        var stranger=Empty(player.GetType());a.GetType("Ow.Game.Objects.Attackable").GetField("<Id>k__BackingField",F).SetValue(stranger,78);
        collected.Invoke(manager,new[]{cargos[2],stranger});Check((int)Get(instance,"CollectedCargo")==2,"another player cannot advance owner tutorial");
        Console.WriteLine("Tutorial cargo lifecycle passed");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
