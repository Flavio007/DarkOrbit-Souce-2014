using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ow.Managers;
using Ow.Managers.MySQLManager;
using Ow.Game.Objects;
using Ow.Game.Objects.Players;
using Ow.Game.Objects.Players.Managers;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
namespace Ow.Game.Events
{
    class AchievementEntry { public int id,done,progress;public int bargainState=3;public bool rewarded; }
    class AchievementDefinition { public int Id,Target,Subject,Value,Amount;public string Key,Event,Title,Objective,Reward,Kind; }
    class AchievementManager
    {
        public static readonly Dictionary<int,AchievementDefinition> Catalog=LoadCatalog();
        private readonly object sync=new object();
        private readonly Dictionary<int,AchievementEntry> entries=new Dictionary<int,AchievementEntry>();
        private readonly Player player;
        private static readonly object SchemaLock=new object();private static bool schemaReady;
        public static void EnsureSchema(){lock(SchemaLock){if(schemaReady)return;using(var db=SqlDatabaseManager.GetClient()){
            if(db.ExecuteQueryRow("SHOW COLUMNS FROM player_accounts LIKE 'achievements'")==null){
                db.ExecuteNonQuery("ALTER TABLE player_accounts ADD COLUMN achievements LONGTEXT NULL");
                if(db.ExecuteQueryRow("SHOW COLUMNS FROM player_accounts LIKE 'achievement'")!=null)db.ExecuteNonQuery("UPDATE player_accounts SET achievements=achievement WHERE achievements IS NULL");
            }
        }schemaReady=true;}}
        public AchievementManager(Player player){this.player=player;}
        private static Dictionary<int,AchievementDefinition> LoadCatalog(){
            var path=new[]{Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"config/achievements.xml"),Path.Combine(Directory.GetCurrentDirectory(),"config/achievements.xml")}.First(File.Exists);
            return XDocument.Load(path).Root.Elements("achievement").Select(e=>new AchievementDefinition{Id=(int)e.Attribute("id"),Target=(int)e.Attribute("target"),Subject=(int?)e.Attribute("subject")??0,Value=(int?)e.Attribute("value")??0,Amount=(int)e.Attribute("amount"),Key=(string)e.Attribute("key"),Event=(string)e.Attribute("event"),Title=(string)e.Attribute("title"),Objective=(string)e.Attribute("objective"),Reward=(string)e.Attribute("reward"),Kind=(string)e.Attribute("kind")}).ToDictionary(d=>d.Id);
        }
        public void Load(string json){lock(sync){entries.Clear();try{foreach(var t in JArray.Parse(string.IsNullOrWhiteSpace(json)?"[]":json)){var e=t.ToObject<AchievementEntry>();if(e==null||!Catalog.ContainsKey(e.id))continue;e.done=e.done>0?1:0;e.progress=e.done>0?Catalog[e.id].Target:Math.Max(0,Math.Min(Catalog[e.id].Target,e.progress));e.bargainState=3;if(t["rewarded"]==null)e.rewarded=e.done>0;entries[e.id]=e;}}catch(Exception e){Logger.Log("error_log","[Achievements] Load: "+e.Message);}}}
        public string Serialize(){lock(sync)return JsonConvert.SerializeObject(entries.Values.OrderBy(e=>e.id));}
        public void EnsureDefaultSeed(){bool changed=false;lock(sync)foreach(var id in Catalog.Keys)if(!entries.ContainsKey(id)){entries[id]=new AchievementEntry{id=id};changed=true;}if(changed)Save();}
        public static bool Matches(AchievementDefinition d,string name,int subject){return d.Event==name&&(d.Subject==0||d.Subject==subject||(name=="level"&&subject>=d.Subject));}
        public void Report(string name,int amount=1,int subject=0){lock(AuctionManager.Sync)ReportCore(name,amount,subject);}
        internal static bool CanReport(string name, int visualMapId) {
            return name != "fly" || visualMapId != TutorialManager.TutorialMapId;
        }
        private void ReportCore(string name,int amount,int subject){if(amount<=0||!CanReport(name,player.Spacemap?.VisualMapId??0))return;lock(sync)foreach(var d in Catalog.Values.Where(x=>Matches(x,name,subject))){AchievementEntry e;if(!entries.TryGetValue(d.Id,out e)||e.done>0)continue;e.progress=(int)Math.Min(d.Target,(long)e.progress+amount);e.done=e.progress>=d.Target?1:0;Save();if(e.done>0){TryReward(e);player.SendPacket("0|A|ACG|"+e.id+"|1|0");player.SendPacket("0|A|STD|Conquista concluida: "+d.Title);}Send(e);}}
        private void Send(AchievementEntry e){player.SendPacket($"0|ach|set|{e.id}|{e.done}|{e.bargainState}");player.SendPacket($"0|A|ACH_PROGRESS|{e.id}|{e.progress}|{Catalog[e.id].Target}|{(e.rewarded?1:0)}");}
        public void SendAll(){lock(AuctionManager.Sync)SendAllCore();}
        private void SendAllCore(){EnsureDefaultSeed();Report("level",1,player.Level);if(player.Group?.Members.Count>=2)Report("group");lock(sync)foreach(var e in entries.Values.OrderBy(x=>x.id)){if(e.done>0&&!e.rewarded)TryReward(e);Send(e);}}
        public void Set(int id,bool done,int bargainState=3,bool sendGain=false){AchievementDefinition d;if(done&&Catalog.TryGetValue(id,out d))Report(d.Event,d.Target,d.Subject);}
        public void Remove(int id){} // Earned progression is permanent.
        public void HandleBuyRequest(int id){if(Catalog.ContainsKey(id))SendAll();} // A client cannot complete objectives.
        private static string Sql(string value){return value.Replace("'","''");}
        private static JArray OwnedShips(JToken token){
            if(token==null)return new JArray();if(token.Type==JTokenType.String){try{token=JToken.Parse((string)token);}catch{return new JArray();}}
            var values=token is JContainer?((JContainer)token).DescendantsAndSelf():new[]{token};
            return new JArray(values.Where(t=>t.Type==JTokenType.Integer).Select(t=>(int)t).Where(id=>id>0).Distinct());
        }
        private void TryReward(AchievementEntry entry){
            if(entry.done==0||entry.rewarded)return;var d=Catalog[entry.id];
            if(!CanReport(d.Event,player.Spacemap?.VisualMapId??0))return;
            try{using(var db=SqlDatabaseManager.GetClient()){
                db.ExecuteNonQuery("START TRANSACTION");
                bool committed=false;try{
                    var account=db.ExecuteQueryRow($"SELECT achievements FROM player_accounts WHERE userId={player.Id} FOR UPDATE");
                    var saved=JArray.Parse(string.IsNullOrWhiteSpace(Convert.ToString(account["achievements"]))?"[]":Convert.ToString(account["achievements"]));
                    if(saved.Any(t=>(int?)t["id"]==entry.id&&(bool?)t["rewarded"]==true)){entry.rewarded=true;db.ExecuteNonQuery("COMMIT");return;}
                    var data=JsonConvert.DeserializeObject<DataBase>(JsonConvert.SerializeObject(player.Data));
                    var ammo=JsonConvert.DeserializeObject<Ammo>(JsonConvert.SerializeObject(player.Ammo));
                    var cargo=JObject.Parse(player.SerializeCargo());List<Drones> drones=null;bool equipShip=false;int shipCredits=0;
                    switch(d.Kind){
                        case "item":db.ExecuteNonQuery($"INSERT INTO player_inventory_items (userId,item_id) VALUES ({player.Id},{d.Value})");break;
                        case "uridium":data.uridium+=d.Amount;break;
                        case "mcb50":ammo.mcb50+=d.Amount;break;
                        case "plt21":ammo.plt21+=d.Amount;break;
                        case "repairCredits":data.repairCredits+=(uint)d.Amount;break;
                        case "promerium":cargo["Promerium"]=(int)cargo["Promerium"]+d.Amount;break;
                        case "ship":
                            var equipment=db.ExecuteQueryRow($"SELECT items FROM player_equipment WHERE userId={player.Id} FOR UPDATE");
                            var items=JObject.Parse(Convert.ToString(equipment["items"]));var ships=OwnedShips(items["ships"]);
                            // Phoenix, Yamato and Defcom precede the Liberator. Leonov and
                            // every later hull are already equivalent or better rewards.
                            if(ships.Any(t=>!new[]{1,2,4}.Contains((int)t)) || (player.Ship!=null&&!new[]{1,2,4}.Contains(player.Ship.Id))){shipCredits=64000;data.credits+=shipCredits;}
                            else {if(!ships.Any(t=>(int?)t==d.Value))ships.Add(d.Value);equipShip=true;}
                            items["ships"]=ships;
                            db.ExecuteNonQuery($"UPDATE player_equipment SET items='{Sql(items.ToString(Formatting.None))}' WHERE userId={player.Id}");break;
                        case "drone":
                            drones=JsonConvert.DeserializeObject<List<Drones>>(JsonConvert.SerializeObject(player.DroneManager.DronesList));
                            if(drones.Count(x=>x.DroneType==1||x.DroneType==2)>=8)throw new InvalidOperationException("No free drone slot");
                            drones.Add(new Drones(drones.Count==0?1:drones.Max(x=>x.Id)+1,1,0,0,1));
                            var droneColumn=db.ExecuteQueryRow("SHOW COLUMNS FROM player_equipment LIKE 'drone'")!=null?"drone":"drones";
                            db.ExecuteNonQuery($"UPDATE player_equipment SET {droneColumn}='{Sql(JsonConvert.SerializeObject(drones))}' WHERE userId={player.Id}");break;
                    }
                    if(equipShip)db.ExecuteNonQuery($"UPDATE player_accounts SET shipId={d.Value} WHERE userId={player.Id}");
                    entry.rewarded=true;
                    db.ExecuteNonQuery($"UPDATE player_accounts SET achievements='{Sql(Serialize())}',data='{Sql(JsonConvert.SerializeObject(data))}',ammo='{Sql(JsonConvert.SerializeObject(ammo))}',cargo='{Sql(cargo.ToString(Formatting.None))}',repairCredits={data.repairCredits} WHERE userId={player.Id}");
                    db.ExecuteNonQuery("COMMIT");committed=true;player.Data=data;player.Ammo=ammo;player.LoadCargo(cargo.ToString(Formatting.None));
                    QueryManager.WriteLogbook(player,"achievement","Conquista concluída: "+d.Title+". Recompensa: "+(shipCredits>0?"64.000 créditos":d.Reward)+".");
                    if(drones!=null)player.DroneManager.DronesList=drones;
                    if(player.GameSession==null)return;
                    if(equipShip)player.ChangeShip(d.Value);
                    if(shipCredits>0)player.SendPacket("0|LM|ST|CRE|"+shipCredits+"|"+data.credits);
                    if(drones!=null)player.DroneManager.UpdateDrones();
                    player.SendOreCount();player.SendCargoStatus();
                    if(d.Kind=="mcb50")player.SettingsManager.SendNewItemStatus(AmmunitionManager.MCB_50);
                    if(d.Kind=="plt21")player.SettingsManager.SendNewItemStatus(AmmunitionManager.PLT_2021);
                    if(d.Kind=="uridium")player.SendPacket("0|LM|ST|URI|"+d.Amount+"|"+data.uridium);
                    player.SendPacket("0|A|STD|Recompensa recebida: "+(shipCredits>0?"64.000 creditos (nave equivalente ou superior ja adquirida)":d.Reward));
                }catch{if(!committed){entry.rewarded=false;db.ExecuteNonQuery("ROLLBACK");}throw;}
            }}catch(Exception e){Logger.Log("error_log","[Achievements] Reward "+entry.id+": "+e);player.SendPacket("0|A|STD|Recompensa pendente: "+d.Reward);}
        }
        public void QuickBuy(string item){
            lock(AuctionManager.Sync)QuickBuyCore(item);
        }
        private void QuickBuyCore(string item){
            var offer=AmmunitionManager.GetQuickBuyOffer(item);
            if(offer==null)return;
            lock(sync){
                bool committed=false;
                try{
                    var data=JsonConvert.DeserializeObject<DataBase>(JsonConvert.SerializeObject(player.Data));
                    var ammo=JsonConvert.DeserializeObject<Ammo>(JsonConvert.SerializeObject(player.Ammo));
                    string error;if(!offer.TryApply(data,ammo,out error)){player.SendPacket("0|A|STD|"+error);return;}
                    using(var db=SqlDatabaseManager.GetClient()){
                        db.ExecuteNonQuery("START TRANSACTION");
                        try{
                            if(db.ExecuteQueryRow($"SELECT userId FROM player_accounts WHERE userId={player.Id} FOR UPDATE")==null)throw new InvalidOperationException("Account missing");
                            db.ExecuteNonQuery($"UPDATE player_accounts SET data='{Sql(JsonConvert.SerializeObject(data))}',ammo='{Sql(JsonConvert.SerializeObject(ammo))}' WHERE userId={player.Id}");
                            db.ExecuteNonQuery("COMMIT");committed=true;
                        }catch{if(!committed)db.ExecuteNonQuery("ROLLBACK");throw;}
                    }
                    player.Data=data;player.Ammo=ammo;
                    player.SettingsManager.SendNewItemStatus(item);
                    player.SendPacket($"0|LM|ST|{(offer.Currency=="credits"?"CRE":"URI")}|{-offer.Price}|{(offer.Currency=="credits"?data.credits:data.uridium)}");
                    player.SendPacket($"0|A|STD|Compra concluida: {offer.Amount} {offer.Name} por {offer.Price} {(offer.Currency=="credits"?"creditos":"Uridium")}.");
                    Report("quickBuy");
                }catch(Exception e){Logger.Log("error_log","[QuickBuy] "+player.Id+" "+item+": "+e);if(!committed)player.SendPacket("0|A|STD|Nao foi possivel concluir a compra. Nenhum valor foi cobrado.");}
            }
        }
        private void Save(){QueryManager.SavePlayer.Achievements(player);}
    }
}
