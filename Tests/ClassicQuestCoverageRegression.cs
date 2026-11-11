using System;
using System.Collections;
using System.Reflection;
class ClassicQuestCoverageRegression {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 static object Get(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);}
 static void Main(string[] args){
 var a=Assembly.LoadFrom(args[0]);var type=a.GetType("Ow.Game.Objects.Players.QuestCatalog",true);
 int total=0,added=0;var ids=new System.Collections.Generic.HashSet<int>();
 foreach(var q in (IEnumerable)type.GetMethod("All",F).Invoke(null,null)) {
  total++;int id=(int)Get(q,"Id");if(!ids.Add(id))throw new Exception("Duplicate quest id");
  var title=(string)Get(q,"Title");if(!title.Contains("crossed the line!")&&title!="David vs. Goliath")continue;
  added++;int playerFaction=(int)Get(q,"FactionId");
  foreach(var c in (IEnumerable)Get(q,"Conditions")) {
   if((string)Get(c,"Type")!="killPlayers"||!(bool)Get(c,"EnemyTarget"))throw new Exception("PvP rule missing");
   if(title.Contains("crossed the line!") && (int)Get(c,"TargetFactionId")==playerFaction)throw new Exception("Friendly faction target");
   if((int)Get(c,"Target")<=0||String.IsNullOrWhiteSpace((string)Get(c,"ShipFamily")))throw new Exception("Invalid kill objective");
  }
  if(title=="David vs. Goliath" && (string)Get(q,"RequiredAmmo")!="ammunition_laser_lcb-10")throw new Exception("Missing x1 restriction");
 }
 if(added!=13||total!=253)throw new Exception("Wrong effective catalog: "+total+" / "+added);
 Console.WriteLine("PASS loaded catalog: 253 quests, 13 new faction-safe definitions, unique IDs and LCB-10 restriction");
 }
}
