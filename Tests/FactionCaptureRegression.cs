using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
class FactionCaptureRegression {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static Assembly a;
 static Type T(string n){return a.GetType(n,true);}
 static object Get(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o,null):o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){var p=o.GetType().GetProperty(n,F);if(p!=null)p.SetValue(o,v,null);else o.GetType().GetField(n,F).SetValue(o,v);}
 static object Empty(string name){var o=FormatterServices.GetUninitializedObject(T(name));for(var t=o.GetType();t!=null;t=t.BaseType)foreach(var f in t.GetFields(F|BindingFlags.DeclaredOnly))if(f.FieldType.IsGenericType&&f.FieldType.GetConstructor(Type.EmptyTypes)!=null&&(f.FieldType.Namespace=="System.Collections.Concurrent"||f.FieldType.Namespace=="System.Collections.Generic"))f.SetValue(o,Activator.CreateInstance(f.FieldType));return o;}
 static object Pos(int x,int y){return Activator.CreateInstance(T("Ow.Game.Movements.Position"),new object[]{x,y});}
 static bool Can(object station,object player){return (bool)station.GetType().GetMethod("CanTradeOre",F).Invoke(station,new[]{player});}
 static void Check(bool b,string m){if(!b)throw new Exception(m);Console.WriteLine("PASS "+m);}
 static int Main(string[] args){try{
 a=Assembly.LoadFrom(args[0]);var map=Empty("Ow.Game.Spacemap");Set(map,"Id",16);
 var station=Empty("Ow.Game.Objects.Stations.BattleStation");Set(station,"Definition",Activator.CreateInstance(T("Ow.Game.Objects.Stations.BattleStationDefinition"),true));Set(station,"Spacemap",map);Set(station,"Position",Pos(1000,1000));Set(station,"FactionId",1);Set(station,"AssetTypeId",(short)36);
 var player=Empty("Ow.Game.Objects.Player");Set(player,"Spacemap",map);Set(player,"Position",Pos(1100,1000));Set(player,"FactionId",1);
 Check(Can(station,player),"owner faction may trade nearby");Set(player,"FactionId",2);Check(!Can(station,player),"enemy cannot trade");Set(player,"FactionId",1);Set(station,"FactionId",0);Check(!Can(station,player),"neutral station cannot trade");Set(station,"FactionId",1);Set(player,"Position",Pos(1800,1000));Check(!Can(station,player),"out of range cannot trade");Set(player,"Position",Pos(1100,1000));Set(station,"AssetTypeId",(short)35);Check(!Can(station,player),"uncaptured asteroid cannot trade");Set(station,"AssetTypeId",(short)36);
 var trade=Empty("Ow.Game.Objects.Stations.OreTradeStation");Set(trade,"Spacemap",map);Set(trade,"Position",Pos(1000,1000));Set(trade,"FactionStation",station);Set(trade,"EmbeddedInHomeStation",true);
 var objects=Get(map,"Activatables");objects.GetType().GetMethod("TryAdd").Invoke(objects,new object[]{123,trade});Check((bool)player.GetType().GetMethod("CanTradeOre",F).Invoke(player,null),"sale authorization uses embedded station");Set(station,"FactionId",2);Check((int)Get(trade,"FactionId")==2,"service follows station ownership including restored stations");Check(!(bool)player.GetType().GetMethod("CanTradeOre",F).Invoke(player,null),"ownership loss revokes sale authorization");Set(station,"FactionId",1);
 if(args.Length>1){Directory.CreateDirectory(args[1]);var cmd=T("Ow.Net.netty.commands.SectorControlBeaconUpdateCommand");File.WriteAllBytes(Path.Combine(args[1],"capture.bin"),(byte[])cmd.GetMethod("write").Invoke(null,new object[]{65,0d,new[]{2,3},2,"sector_control_16_123"}));File.WriteAllBytes(Path.Combine(args[1],"visibility.bin"),(byte[])T("Ow.Net.netty.commands.SectorControlBeaconProgressVisibilityCommand").GetMethod("write").Invoke(null,new object[]{"sector_control_16_123",true}));File.WriteAllBytes(Path.Combine(args[1],"remove.bin"),(byte[])T("Ow.Net.netty.commands.MapRemovePOICommand").GetMethod("write").Invoke(null,new object[]{"sector_control_16_123"}));}
 return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
