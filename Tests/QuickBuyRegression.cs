using System;
using System.Reflection;
using System.Runtime.Serialization;
class QuickBuyRegression {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static object Get(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o,null):o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){var p=o.GetType().GetProperty(n,F);if(p!=null)p.SetValue(o,Convert.ChangeType(v,p.PropertyType),null);else{var f=o.GetType().GetField(n,F);f.SetValue(o,Convert.ChangeType(v,f.FieldType));}}
 static void Check(bool b,string msg){if(!b)throw new Exception(msg);}
 static int Main(string[] args){try{
 var a=Assembly.LoadFrom(args[0]);var type=a.GetType("Ow.Game.Objects.Players.Managers.AmmunitionManager");var offers=(Array)type.GetField("QuickBuyOffers").GetValue(null);Check(offers.Length==9,"catalog count");
 var manager=FormatterServices.GetUninitializedObject(a.GetType("Ow.Game.Objects.Players.Managers.SettingsManager"));
 foreach(var o in offers){string id=(string)Get(o,"Id"),currency=(string)Get(o,"Currency"),prop=(string)Get(o,"AmmoProperty");int amount=(int)Get(o,"Amount");long price=(long)Get(o,"Price");
 Check(!id.Contains("rocketlauncher")&&id!="ammunition_laser_ucb-100","restricted ammunition");Check(amount==(id.StartsWith("ammunition_rocket_")?100:1000),"pack size");
 var apply=o.GetType().GetMethod("TryApply");var pars=apply.GetParameters();var data=FormatterServices.GetUninitializedObject(pars[0].ParameterType);var ammo=FormatterServices.GetUninitializedObject(pars[1].ParameterType);
 Set(data,"credits",12345678L);Set(data,"uridium",234567L);Set(data,currency,price-1);Set(ammo,prop,15);
 object[] call={data,ammo,null};Check(!(bool)apply.Invoke(o,call),"insufficient funds accepted");Check(Convert.ToInt64(Get(data,currency))==price-1&&(int)Get(ammo,prop)==15,"failed purchase mutated state");
 Set(data,currency,price);call=new object[]{data,ammo,null};Check((bool)apply.Invoke(o,call),"valid purchase rejected");Check(Convert.ToInt64(Get(data,currency))==0&&(int)Get(ammo,prop)==15+amount,"incorrect debit/reward");Check(Convert.ToInt64(Get(data,currency=="credits"?"uridium":"credits"))==(currency=="credits"?234567L:12345678L),"wrong currency changed");
 Set(data,currency,price);Set(ammo,prop,int.MaxValue-amount+1);call=new object[]{data,ammo,null};Check(!(bool)apply.Invoke(o,call)&&Convert.ToInt64(Get(data,currency))==price,"overflow charged balance");
 var status=manager.GetType().GetMethod("GetBuyItemStatus").Invoke(manager,new object[]{id});Check((string)Get(status,"var_1474")=="quickbuy_"+id&&(string)Get(status,"iconLootId")==id,"buy entry overwrites ammo identity");Check((double)Get(status,"counterValue")==amount&&(double)Get(status,"maxCounterValue")==price,"wrong client quote");
 Console.WriteLine("PASS "+Get(o,"Name")+": "+amount+" / "+price+" "+currency);
 }
 Check(type.GetMethod("GetQuickBuyOffer").Invoke(null,new object[]{"ammunition_rocketlauncher_eco-10"})==null,"launcher accepted");Check(type.GetMethod("GetQuickBuyOffer").Invoke(null,new object[]{"invalid"})==null,"unknown accepted");
 Console.WriteLine("Quick-buy catalog, pricing, balances, overflow, packs and independent icons passed");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
