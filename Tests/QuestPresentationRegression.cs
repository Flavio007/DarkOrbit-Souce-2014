using System;
using System.Reflection;
class QuestPresentationRegression {
 static void Main(string[] args) {
  var a=Assembly.LoadFrom(args[0]);var t=a.GetType("Ow.Game.Objects.Players.QuestConditionDefinition",true);
  var method=a.GetType("Ow.Game.Objects.Players.Quests",true).GetMethod("ObjectiveDescription",BindingFlags.Static|BindingFlags.NonPublic);
  var c=Activator.CreateInstance(t,true);
  t.GetProperty("MapRelation").SetValue(c,"home",null);t.GetProperty("Type").SetValue(c,"visitPosition",null);
  t.GetProperty("DescriptionKey").SetValue(c,"Etapa 1: Voe até as coordenadas  (1) 107/71 / X-1 / território da sua companhia",null);
  for(int f=1;f<=3;f++) {
   var result=(string)method.Invoke(null,new object[]{c,f});
   if(result!="Etapa 1: Voe até as coordenadas 107/71 / "+f+"-1")throw new Exception(result);
  }
  t.GetProperty("MapRelation").SetValue(c,"enemy",null);
  var enemy=(string)method.Invoke(null,new object[]{c,1});if(!enemy.Contains("X-1"))throw new Exception("Enemy map incorrectly resolved");
  Console.WriteLine("PASS: three company maps, coordinate pair, stage and enemy-map semantics");
 }
}
