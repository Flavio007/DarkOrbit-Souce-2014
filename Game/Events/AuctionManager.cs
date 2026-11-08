using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ow.Managers;
using Ow.Managers.MySQLManager;
using Ow.Game.Objects;
using Ow.Game.Objects.Players;
using Ow.Game.Objects.Players.Managers;
using Ow.Utils;

namespace Ow.Game.Events
{
    class AuctionItem
    {
        public string key,name,category,kind,value,cycle,currency,image;
        public int amount,price,baseShipId;
    }
    // The site never writes balances or grants prizes. All mutations are made by
    // this service, serialized with login and the live player's economy updates.
    static class AuctionManager
    {
        public static readonly object Sync = new object();
        public static readonly Dictionary<string,AuctionItem> Catalog = JsonConvert.DeserializeObject<List<AuctionItem>>(File.ReadAllText(new[]{Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"config/auctions.json"),Path.Combine(Directory.GetCurrentDirectory(),"config/auctions.json")}.First(File.Exists))).ToDictionary(x=>x.key);
        private static Timer timer;
        private static bool schemaReady;
        private static string droneColumn;
        private static readonly Dictionary<string,long> seeded=new Dictionary<string,long>();
        private const long MaxBid = 9007199254740991;
        private static long Unix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc,DateTimeKind.Utc)).ToUnixTimeSeconds();
        public static long[] Bounds(string cycle,DateTime utc)
        {
            var local=utc.AddHours(-3); // Server calendar: America/Bahia, UTC-3.
            var start=cycle=="hourly"?local.Date.AddHours(local.Hour):local.Date;
            if(cycle=="weekly")start=start.AddDays(-(((int)local.DayOfWeek+6)%7));
            var end=cycle=="hourly"?start.AddHours(1):cycle=="daily"?start.AddDays(1):start.AddDays(7);
            return new[]{Unix(start.AddHours(3)),Unix(end.AddHours(3))};
        }
        public static void EnsureSchema()
        {
            lock(Sync){if(schemaReady)return;using(var db=SqlDatabaseManager.GetClient()){
                droneColumn=db.ExecuteQueryRow("SHOW COLUMNS FROM player_equipment LIKE 'drone'")!=null?"drone":"drones";
                db.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS auction_lots (id BIGINT PRIMARY KEY AUTO_INCREMENT,item_key VARCHAR(64) NOT NULL,cycle VARCHAR(8) NOT NULL,starts BIGINT NOT NULL,ends BIGINT NOT NULL,top_user INT NOT NULL DEFAULT 0,top_bid BIGINT NOT NULL DEFAULT 0,status VARCHAR(16) NOT NULL DEFAULT 'open',UNIQUE KEY round_item(item_key,starts),KEY closing(status,ends)) ENGINE=InnoDB");
                db.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS auction_bids (id BIGINT PRIMARY KEY AUTO_INCREMENT,lot_id BIGINT NOT NULL,user_id INT NOT NULL,amount BIGINT NOT NULL,created BIGINT NOT NULL,KEY personal(user_id,lot_id)) ENGINE=InnoDB");
                db.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS auction_requests (user_id INT NOT NULL,request_id VARCHAR(64) NOT NULL,response TEXT NOT NULL,PRIMARY KEY(user_id,request_id)) ENGINE=InnoDB");
                db.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS auction_messages (id BIGINT PRIMARY KEY AUTO_INCREMENT,user_id INT NOT NULL,lot_id BIGINT NOT NULL,message TEXT NOT NULL,created BIGINT NOT NULL,is_read TINYINT NOT NULL DEFAULT 0,KEY inbox(user_id,id)) ENGINE=InnoDB");
            }schemaReady=true;}
        }
        public static void Start()
        {
            EnsureSchema();
            timer=new Timer(_=>{if(!Monitor.TryEnter(Sync))return;try{Advance(DateTime.UtcNow);}catch(Exception e){Logger.Log("error_log","[Auction] "+e);}finally{Monitor.Exit(Sync);}},null,0,1000);
        }
        public static void Advance(DateTime now)
        {
            lock(Sync){EnsureSchema();using(var db=SqlDatabaseManager.GetClient()){
                var expired=db.ExecuteQueryTable($"SELECT id FROM auction_lots WHERE status='open' AND ends<={Unix(now)} ORDER BY id");
                foreach(DataRow r in expired.Rows)Settle(Convert.ToInt64(r["id"]));
                foreach(var cycle in new[]{"hourly","daily","weekly"}){
                    var b=Bounds(cycle,now);
                    if(seeded.ContainsKey(cycle)&&seeded[cycle]==b[0])continue;
                    var values=Catalog.Values.Where(x=>x.cycle==cycle).Select(x=>$"('{S(x.key)}','{cycle}',{b[0]},{b[1]})").ToArray();
                    if(values.Length>0)db.ExecuteNonQuery("INSERT IGNORE INTO auction_lots(item_key,cycle,starts,ends) VALUES "+string.Join(",",values));
                    seeded[cycle]=b[0];
                }
            }}
        }
        private static string S(string s)=>(s??"").Replace("\\","\\\\").Replace("'","''");
        private static string Json(object o)=>JsonConvert.SerializeObject(o);
        private static JObject Parse(object o){var s=Convert.ToString(o);return JObject.Parse(string.IsNullOrWhiteSpace(s)?"{}":s);}
        private static Player Online(int id)=>GameManager.GetPlayerById(id);
        private static void Message(SqlDatabaseClient db,int user,long lot,string text){db.ExecuteNonQuery($"INSERT INTO auction_messages(user_id,lot_id,message,created) VALUES({user},{lot},'{S(text)}',{Unix(DateTime.UtcNow)})");}
        private static string Fail(string text)=>Json(new{ok=false,message=text});
        private static long Balance(SqlDatabaseClient db,int user,string currency)
        {
            var row=db.ExecuteQueryRow($"SELECT data FROM player_accounts WHERE userId={user} FOR UPDATE");
            if(row==null)throw new InvalidOperationException("Conta inexistente.");
            var p=Online(user);var data=p!=null?JObject.FromObject(p.Data):Parse(row["data"]);
            return (long?)data[currency]??0;
        }
        private static void Money(SqlDatabaseClient db,int user,string currency,long delta,List<Action> after)
        {
            var row=db.ExecuteQueryRow($"SELECT data FROM player_accounts WHERE userId={user} FOR UPDATE");
            if(row==null)throw new InvalidOperationException("Conta inexistente.");
            var p=Online(user);var data=p!=null?JObject.FromObject(p.Data):Parse(row["data"]);
            long total=checked(((long?)data[currency]??0)+delta);
            if(total<0)throw new InvalidOperationException("Saldo insuficiente.");
            data[currency]=total;
            db.ExecuteNonQuery($"UPDATE player_accounts SET data='{S(data.ToString(Formatting.None))}' WHERE userId={user}");
            after.Add(()=>{if(p!=null){if(currency=="credits")p.Data.credits=total;else p.Data.uridium=total;p.SendPacket($"0|LM|ST|{(currency=="credits"?"CRE":"URI")}|{delta}|{total}");}});
        }
        private static HashSet<int> Ids(JToken token)
        {
            if(token==null)return new HashSet<int>();
            if(token.Type==JTokenType.String){try{token=JToken.Parse((string)token);}catch{return new HashSet<int>();}}
            var all=token is JContainer?((JContainer)token).DescendantsAndSelf():new[]{token};
            return new HashSet<int>(all.Where(x=>x.Type==JTokenType.Integer).Select(x=>(int)x));
        }
        private static void Eligible(SqlDatabaseClient db,int user,AuctionItem item)
        {
            var row=db.ExecuteQueryRow($"SELECT items,{droneColumn} AS drone FROM player_equipment WHERE userId={user} FOR UPDATE");
            if(row==null)throw new InvalidOperationException("Equipamento indisponivel.");
            var items=Parse(row["items"]);
            if(item.kind=="ship"||item.kind=="design"){
                var id=int.Parse(item.value);var ids=Ids(items[item.kind=="design"?"designs":"ships"]);
                if(ids.Contains(id)||Online(user)?.Ship?.Id==id)throw new InvalidOperationException("Voce ja possui esta nave ou design.");
            }
            if(item.kind=="drone"){
                var drones=Online(user)?.DroneManager?.DronesList??JsonConvert.DeserializeObject<List<Drones>>(Convert.ToString(row["drone"]))??new List<Drones>();
                if(drones.Count(x=>x.DroneType==1||x.DroneType==2)>=8)throw new InvalidOperationException("Todos os 8 slots de drones estao ocupados.");
            }
            if(item.kind=="item"){
                var limit=db.ExecuteQueryRow($"SELECT max_quantity FROM inventory_item_definitions WHERE item_id={int.Parse(item.value)} AND is_active=1");
                if(limit==null)throw new InvalidOperationException("Item indisponivel.");
                var count=db.ExecuteQueryRow($"SELECT COUNT(*) n FROM player_inventory_items WHERE userId={user} AND item_id={int.Parse(item.value)}");
                if(Convert.ToInt32(limit["max_quantity"])>0&&Convert.ToInt32(count["n"])>=Convert.ToInt32(limit["max_quantity"]))throw new InvalidOperationException("Limite de unidades deste item atingido.");
            }
            if(item.kind=="ammo"){
                var ammo=Online(user)?.Ammo;
                var rowAmmo=db.ExecuteQueryRow($"SELECT ammo FROM player_accounts WHERE userId={user} FOR UPDATE");
                var amount=ammo!=null?Convert.ToInt32(typeof(Ammo).GetField(item.value).GetValue(ammo)):((int?)Parse(rowAmmo["ammo"])[item.value]??0);
                if(amount>int.MaxValue-item.amount)throw new InvalidOperationException("Limite de municao atingido.");
            }
        }
        private static void Prize(SqlDatabaseClient db,int user,AuctionItem item,List<Action> after)
        {
            Eligible(db,user,item);var p=Online(user);
            if(item.kind=="item")db.ExecuteNonQuery($"INSERT INTO player_inventory_items(userId,item_id) VALUES({user},{int.Parse(item.value)})");
            else if(item.kind=="ammo"){
                var row=db.ExecuteQueryRow($"SELECT ammo FROM player_accounts WHERE userId={user} FOR UPDATE");
                var ammo=p!=null?JObject.FromObject(p.Ammo):Parse(row["ammo"]);var count=checked(((int?)ammo[item.value]??0)+item.amount);ammo[item.value]=count;
                db.ExecuteNonQuery($"UPDATE player_accounts SET ammo='{S(ammo.ToString(Formatting.None))}' WHERE userId={user}");
                after.Add(()=>{if(p!=null){typeof(Ammo).GetField(item.value).SetValue(p.Ammo,count);p.SettingsManager.SendNewItemStatus(AmmoCode(item.value));}});
            }else if(item.kind=="booster"){
                var row=db.ExecuteQueryRow($"SELECT boosters FROM player_equipment WHERE userId={user} FOR UPDATE");
                var boosters=p?.BoosterManager!=null?JsonConvert.DeserializeObject<Dictionary<short,List<BoosterBase>>>(Json(p.BoosterManager.Boosters)):JsonConvert.DeserializeObject<Dictionary<short,List<BoosterBase>>>(Convert.ToString(row["boosters"]))??new Dictionary<short,List<BoosterBase>>();
                var type=(short)(BoosterType)Enum.Parse(typeof(BoosterType),item.value);var group=BoosterManager.GetBoostedAttributeType(type);
                if(!boosters.ContainsKey(group))boosters[group]=new List<BoosterBase>();var boost=boosters[group].FirstOrDefault(x=>x.Type==type);
                if(boost==null)boosters[group].Add(new BoosterBase(type,item.amount*3600));else boost.Seconds=checked(boost.Seconds+item.amount*3600);
                db.ExecuteNonQuery($"UPDATE player_equipment SET boosters='{S(Json(boosters))}' WHERE userId={user}");
                after.Add(()=>{if(p!=null){p.BoosterManager.Load(boosters);p.BoosterManager.Update();}});
            }else{
                var row=db.ExecuteQueryRow($"SELECT items,{droneColumn} AS drone FROM player_equipment WHERE userId={user} FOR UPDATE");var items=Parse(row["items"]);
                if(item.kind=="ship"){
                    var shipId=int.Parse(item.value);
                    var ships=Ids(items["ships"]);ships.Add(shipId);items["ships"]=JArray.FromObject(ships);
                    // A hull is activated just like a shop purchase, not only stored in
                    // the ownership list. Persist it in the prize transaction for offline users.
                    db.ExecuteNonQuery($"UPDATE player_accounts SET shipId={shipId} WHERE userId={user}");
                    after.Add(()=>{if(p?.GameSession!=null)p.ChangeShip(shipId);});
                }
                if(item.kind=="design"){
                    var designs=items["designs"] as JObject??new JObject();var list=Ids(designs[item.baseShipId.ToString()]);list.Add(int.Parse(item.value));designs[item.baseShipId.ToString()]=JArray.FromObject(list);items["designs"]=designs;
                }
                if(item.kind=="logdisk"){var skill=items["skillTree"] as JObject??new JObject();skill["logdisks"]=checked(((int?)skill["logdisks"]??0)+item.amount);items["skillTree"]=skill;}
                if(item.kind=="drone"){
                    var drones=p?.DroneManager!=null?JsonConvert.DeserializeObject<List<Drones>>(Json(p.DroneManager.DronesList)):JsonConvert.DeserializeObject<List<Drones>>(Convert.ToString(row["drone"]))??new List<Drones>();
                    drones.Add(new Drones(drones.Count==0?1:drones.Max(x=>x.Id)+1,2,0,0,1));
                    db.ExecuteNonQuery($"UPDATE player_equipment SET {droneColumn}='{S(Json(drones))}' WHERE userId={user}");
                    after.Add(()=>{if(p!=null){p.DroneManager.DronesList=drones;p.DroneManager.UpdateDrones();}});
                }
                db.ExecuteNonQuery($"UPDATE player_equipment SET items='{S(items.ToString(Formatting.None))}' WHERE userId={user}");
            }
        }
        private static string AmmoCode(string key){return new Dictionary<string,string>{{"mcb25",AmmunitionManager.MCB_25},{"mcb50",AmmunitionManager.MCB_50},{"sab50",AmmunitionManager.SAB_50},{"plt21",AmmunitionManager.PLT_2021},{"plt3030",AmmunitionManager.PLT_3030}}[key];}
        private static void Complete(List<Action> actions){foreach(var a in actions)try{a();}catch(Exception e){Logger.Log("error_log","[Auction] Post-commit notification: "+e);}}
        public static string Request(int user,long lotId,long amount,string requestId,bool buy=false)
        {
            if(user<=0||lotId<=0||!System.Text.RegularExpressions.Regex.IsMatch(requestId??"","^[a-zA-Z0-9-]{16,64}$"))return Fail("Requisicao invalida.");
            lock(Sync){EnsureSchema();Advance(DateTime.UtcNow);using(var db=SqlDatabaseManager.GetClient()){
                db.ExecuteNonQuery("START TRANSACTION");var after=new List<Action>();
                try{
                    var previous=db.ExecuteQueryRow($"SELECT response FROM auction_requests WHERE user_id={user} AND request_id='{S(requestId)}' FOR UPDATE");
                    if(previous!=null){db.ExecuteNonQuery("COMMIT");return Convert.ToString(previous["response"]);}
                    var row=db.ExecuteQueryRow($"SELECT * FROM auction_lots WHERE id={lotId} FOR UPDATE");
                    if(row==null||(string)row["status"]!="open"||Convert.ToInt64(row["ends"])<=Unix(DateTime.UtcNow))throw new InvalidOperationException("Este leilao ja terminou. Atualize a lista.");
                    var item=Catalog[(string)row["item_key"]];Eligible(db,user,item);
                    if(buy){
                        if(item.price<=0)throw new InvalidOperationException("Compra indisponivel.");
                        Money(db,user,item.currency,-(long)item.price,after);Prize(db,user,item,after);
                        Message(db,user,lotId,"Compra imediata: "+item.name+". Seu lance no leilao permanece ativo.");
                    }else{
                        var top=Convert.ToInt64(row["top_bid"]);var oldUser=Convert.ToInt32(row["top_user"]);
                        if(amount<top+10000||amount>MaxBid)throw new InvalidOperationException("O lance deve superar o atual em pelo menos 10.000 creditos.");
                        var charge=oldUser==user?amount-top:amount;
                        // Lock both accounts in ascending order, independent of who bids.
                        foreach(var id in new[]{user,oldUser}.Where(x=>x>0).Distinct().OrderBy(x=>x))db.ExecuteQueryRow($"SELECT userId FROM player_accounts WHERE userId={id} FOR UPDATE");
                        Money(db,user,"credits",-charge,after);
                        if(oldUser>0&&oldUser!=user){Money(db,oldUser,"credits",top,after);Message(db,oldUser,lotId,"Lance superado em "+item.name+". "+top+" creditos devolvidos.");}
                        db.ExecuteNonQuery($"UPDATE auction_lots SET top_user={user},top_bid={amount} WHERE id={lotId}");
                        db.ExecuteNonQuery($"INSERT INTO auction_bids(lot_id,user_id,amount,created) VALUES({lotId},{user},{amount},{Unix(DateTime.UtcNow)})");
                    }
                    var response=Json(new{ok=true,message=buy?"Compra realizada. Item entregue.":"Lance aceito. Voce e o maior licitante."});
                    db.ExecuteNonQuery($"INSERT INTO auction_requests(user_id,request_id,response) VALUES({user},'{S(requestId)}','{S(response)}')");
                    db.ExecuteNonQuery("COMMIT");Complete(after);return response;
                }catch(Exception e){db.ExecuteNonQuery("ROLLBACK");if(!(e is InvalidOperationException||e is OverflowException))Logger.Log("error_log","[Auction] Bid: "+e);return Fail(e is InvalidOperationException?e.Message:"Nao foi possivel concluir. Tente novamente com a mesma requisicao.");}
            }}
        }
        private static void Settle(long id)
        {
            using(var db=SqlDatabaseManager.GetClient()){
                db.ExecuteNonQuery("START TRANSACTION");var after=new List<Action>();
                try{
                    var row=db.ExecuteQueryRow($"SELECT * FROM auction_lots WHERE id={id} FOR UPDATE");if(row==null||(string)row["status"]!="open"){db.ExecuteNonQuery("COMMIT");return;}
                    var user=Convert.ToInt32(row["top_user"]);var item=Catalog[(string)row["item_key"]];var status="closed";
                    if(user>0){
                        try{Eligible(db,user,item);}catch(InvalidOperationException){status="refunded";}
                        if(status=="refunded"){Money(db,user,"credits",Convert.ToInt64(row["top_bid"]),after);Message(db,user,id,"Nao foi possivel entregar "+item.name+" (item ja adquirido ou limite atingido). Lance devolvido integralmente.");}
                        else{Prize(db,user,item,after);Message(db,user,id,"Voce venceu: "+item.name+" x"+item.amount+". Item entregue ao inventario.");var p=Online(user);if(p!=null)after.Add(()=>p.SendPacket("0|A|STD|Leilao vencido: "+item.name));}
                    }
                    db.ExecuteNonQuery($"UPDATE auction_lots SET status='{status}' WHERE id={id}");db.ExecuteNonQuery("COMMIT");Complete(after);
                }catch{db.ExecuteNonQuery("ROLLBACK");throw;}
            }
        }
    }
}
