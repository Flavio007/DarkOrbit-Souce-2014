using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
class MineralVeinsRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Set(object o,string n,object v){var p=o.GetType().GetProperty(n,F);if(p!=null)p.SetValue(o,v,null);else o.GetType().GetField(n,F).SetValue(o,v);}
 static int Coord(object p,string n){var f=p.GetType().GetField(n,F);return (int)(f!=null?f.GetValue(p):p.GetType().GetProperty(n,F).GetValue(p,null));}
 static double D(object a,object b){double x=Coord(a,"X")-Coord(b,"X"),y=Coord(a,"Y")-Coord(b,"Y");return x*x+y*y;}
 static void Check(bool b,string msg){if(!b)throw new Exception(msg);}
 static int Main(string[] args){try {
 var a=Assembly.LoadFrom(args[0]);var mt=a.GetType("Ow.Game.Spacemap");var pt=a.GetType("Ow.Game.Movements.Position");var at=a.GetType("Ow.Game.Objects.Activatable");
 var map=FormatterServices.GetUninitializedObject(mt);Set(map,"Id",1);
 var dict=Activator.CreateInstance(typeof(System.Collections.Concurrent.ConcurrentDictionary<,>).MakeGenericType(typeof(int),at));Set(map,"Activatables",dict);
 var portal=FormatterServices.GetUninitializedObject(a.GetType("Ow.Game.Objects.Portal"));var pp=Activator.CreateInstance(pt,new object[]{10400,6400});Set(portal,"Position",pp);Set(portal,"Visible",true);
 dict.GetType().GetMethod("TryAdd").Invoke(dict,new object[]{1,portal});
 var vt=typeof(Tuple<,>).MakeGenericType(typeof(int),pt);var veins=Activator.CreateInstance(typeof(List<>).MakeGenericType(vt));var occupied=Activator.CreateInstance(typeof(List<>).MakeGenericType(pt));
 var generate=mt.GetMethod("GenerateMineralVeins",F);var groups=new List<IList>();
 for(int type=1;type<=3;type++){
 var points=(IList)generate.Invoke(map,new object[]{type,120,0,20800,0,12800,veins,occupied});Check(points.Count==120,"configured count lost");groups.Add(points);
 foreach(var p in points){Check(Coord(p,"X")>=0&&Coord(p,"X")<=20800&&Coord(p,"Y")>=0&&Coord(p,"Y")<=12800,"bounds");Check(D(p,pp)>=1800*1800,"portal clearance");}
 }
 var all=(IList)occupied;for(int i=0;i<all.Count;i++)for(int j=0;j<i;j++)Check(D(all[i],all[j])>=85*85,"overlap");
 foreach(var group in groups)foreach(var p in group){int neighbors=0;foreach(var q in group)if(D(p,q)<=960*960)neighbors++;Check(neighbors>=8,"isolated deposit");}
 for(int i=0;i<groups.Count;i++)for(int j=0;j<i;j++)foreach(var p in groups[i])foreach(var q in groups[j])Check(D(p,q)>=340*340,"mineral types mixed");
 var cramped=(IList)generate.Invoke(map,new object[]{1,10,10400,10400,6400,6400,veins,occupied});Check(cramped.Count==0,"unsafe fallback");
 Console.WriteLine("PASS mineral veins: counts, bounds, portal exclusion, spacing, same-type groups, separate types, impossible-region termination");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
