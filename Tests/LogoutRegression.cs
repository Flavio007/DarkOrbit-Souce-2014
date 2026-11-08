using System;
using System.Reflection;
using System.Runtime.Serialization;
class LogoutRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static Assembly a;
 static object Get(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o,null):o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){var p=o.GetType().GetProperty(n,F);if(p!=null)p.SetValue(o,v,null);else o.GetType().GetField(n,F).SetValue(o,v);}
 static object Empty(string name){var o=FormatterServices.GetUninitializedObject(a.GetType(name,true));for(var t=o.GetType();t!=null;t=t.BaseType)foreach(var f in t.GetFields(F|BindingFlags.DeclaredOnly))if(f.FieldType.IsGenericType&&f.FieldType.GetConstructor(Type.EmptyTypes)!=null&&(f.FieldType.Namespace=="System.Collections.Concurrent"||f.FieldType.Namespace=="System.Collections.Generic"))f.SetValue(o,Activator.CreateInstance(f.FieldType));return o;}
 static void Check(bool b,string m){if(!b)throw new Exception(m);Console.WriteLine("PASS "+m);}
 static void Logout(object p,bool start){p.GetType().GetMethod("Logout").Invoke(p,new object[]{start});}
 static int Main(string[] args){try{
  a=Assembly.LoadFrom(args[0]);var p=Empty("Ow.Game.Objects.Player");Set(p,"LogoutSync",new object());
  Set(p,"Storage",Empty("Ow.Game.Objects.Storage"));Set(p,"AttackManager",Empty("Ow.Game.Objects.Players.Managers.AttackManager"));
  var map=Empty("Ow.Game.Spacemap");Set(map,"Id",1);var options=Empty("Ow.Game.OptionsBase");Set(map,"Options",options);Set(p,"Spacemap",map);
  Logout(p,true);Check((bool)Get(p,"LoggingOut")&&(int)Get(p,"LogoutLastSeconds")==10,"server starts ten second countdown");
  var began=(DateTime)Get(p,"LogoutStartTime");Logout(p,true);Check((DateTime)Get(p,"LogoutStartTime")==began,"duplicate request does not restart countdown");
  Set(Get(p,"Storage"),"IsInDemilitarizedZone",true);Set(p,"LastCombatTime",DateTime.Now);Logout(p,false);Check(!(bool)Get(p,"LoggingOut"),"combat cancels even in neutral zone");Set(p,"LastCombatTime",DateTime.MinValue);
  Set(p,"Moving",true);Logout(p,true);Check(!(bool)Get(p,"LoggingOut"),"movement blocks logout");Set(p,"Moving",false);
  Set(options,"LogoutBlocked",true);Logout(p,true);Check(!(bool)Get(p,"LoggingOut"),"map policy blocks logout");Set(options,"LogoutBlocked",false);
  Logout(p,true);Set(map,"Id",2);Logout(p,false);Check(!(bool)Get(p,"LoggingOut"),"map transition cancels logout");
  Set(Get(p,"Storage"),"Jumping",true);Logout(p,true);Check(!(bool)Get(p,"LoggingOut"),"portal jump blocks logout");Set(Get(p,"Storage"),"Jumping",false);
  Set(p,"Premium",true);Logout(p,true);Check((int)Get(p,"LogoutLastSeconds")==5,"premium countdown uses server policy");
  p.GetType().GetMethod("AbortLogout").Invoke(p,new object[]{"user"});Check(!(bool)Get(p,"LoggingOut"),"manual cancellation");
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
