using System;
using System.Reflection;
using System.Runtime.Serialization;
class ReadyPodRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static Assembly a;
 static object Get(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o,null):o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){var p=o.GetType().GetProperty(n,F);if(p!=null)p.SetValue(o,v,null);else o.GetType().GetField(n,F).SetValue(o,v);}
 static object Empty(string name){var o=FormatterServices.GetUninitializedObject(a.GetType(name,true));for(var t=o.GetType();t!=null;t=t.BaseType)foreach(var f in t.GetFields(F|BindingFlags.DeclaredOnly))if(f.FieldType.IsGenericType&&f.FieldType.GetConstructor(Type.EmptyTypes)!=null&&(f.FieldType.Namespace=="System.Collections.Concurrent"||f.FieldType.Namespace=="System.Collections.Generic"))f.SetValue(o,Activator.CreateInstance(f.FieldType));return o;}
 static void Check(bool b,string message){if(!b)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static bool Ready(object p){var d=Get(p,"VisualModifiers");return (bool)d.GetType().GetMethod("ContainsKey").Invoke(d,new object[]{(short)1});}
 static void Tick(object skill){skill.GetType().GetMethod("Tick").Invoke(skill,null);}
 static void Test(string name,int shipId){
  var player=Empty("Ow.Game.Objects.Player");
  var ship=Empty("Ow.Game.Ship");Set(ship,"Id",shipId);Set(ship,"LootId",shipId==49?"ship_aegis":"ship_citadel");Set(player,"Ship",ship);
  Set(player,"Storage",Empty("Ow.Game.Objects.Storage"));Set(player,"BoosterManager",Empty("Ow.Game.Objects.Players.Managers.BoosterManager"));
  var skill=Empty("Ow.Game.Objects.Players.Skills."+name);Set(skill,"Player",player);
  var skills=Get(Get(player,"Storage"),"Skills");var loot=(string)Get(skill,"LootId");skills.GetType().GetMethod("Add").Invoke(skills,new object[]{loot,skill});
  Tick(skill);Check(Ready(player),name+" ready announces mounted pod");
  player.GetType().GetMethod("RemoveVisualModifier").Invoke(player,new object[]{1});Set(skill,"cooldown",DateTime.Now);Tick(skill);Check(!Ready(player),name+" cooldown hides pod");
  Set(skill,"cooldown",DateTime.MinValue);Tick(skill);Check(Ready(player),name+" cooldown expiry restores pod");
  player.GetType().GetMethod("RemoveVisualModifier").Invoke(player,new object[]{1});skills.GetType().GetMethod("Clear").Invoke(skills,null);Tick(skill);Check(!Ready(player),name+" obsolete skill cannot restore pod after ship change");
 }
 static int Main(string[] args){try{a=Assembly.LoadFrom(args[0]);Test("AegisRepairPod",49);Test("Fortify",69);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
