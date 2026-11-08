param([string]$AssemblyPath='H:\Darkorbit-Cpp\build\server-auction\DarkOrbit.exe')
$ErrorActionPreference='Stop'
[Environment]::CurrentDirectory=(Split-Path $PSScriptRoot -Parent)
$asm=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$null=[Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $AssemblyPath) 'MySql.Data.dll'))
$connection=New-Object MySql.Data.MySqlClient.MySqlConnection 'Server=127.0.0.1;User ID=root;SslMode=None'
$connection.Open();$dbName='do_auction_test_'+[Guid]::NewGuid().ToString('N')
function Query([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
$database=$asm.GetType('Ow.Managers.MySQLManager.SqlDatabaseManager');$field=$database.GetField('ConnectionString');$original=$field.GetValue($null)
$type=$asm.GetType('Ow.Game.Events.AuctionManager');$advance=$type.GetMethod('Advance');$request=$type.GetMethod('Request')
function Request([int]$user,[long]$lot,[long]$amount,[string]$key=[Guid]::NewGuid().ToString('N'),[bool]$buy=$false){return ($request.Invoke($null,@($user,$lot,$amount,$key,$buy))|ConvertFrom-Json)}
function Credits([int]$user){return [long](ConvertFrom-Json (Query "SELECT data FROM player_accounts WHERE userId=$user")).credits}
try{
 $null=Query "CREATE DATABASE $dbName";$connection.ChangeDatabase($dbName)
 $null=Query 'CREATE TABLE player_accounts(userId INT PRIMARY KEY,data LONGTEXT,ammo LONGTEXT) ENGINE=InnoDB'
 $null=Query 'CREATE TABLE player_equipment(userId INT PRIMARY KEY,items LONGTEXT,drone LONGTEXT,boosters LONGTEXT) ENGINE=InnoDB'
 $null=Query 'CREATE TABLE inventory_item_definitions(item_id INT PRIMARY KEY,max_quantity INT,is_active INT) ENGINE=InnoDB'
 $null=Query 'CREATE TABLE player_inventory_items(id BIGINT PRIMARY KEY AUTO_INCREMENT,userId INT,item_id INT) ENGINE=InnoDB'
 foreach($user in 1,2){$null=Query "INSERT INTO player_accounts VALUES($user,'{`"credits`":1000000,`"uridium`":10000000}','{}')";$null=Query "INSERT INTO player_equipment VALUES($user,'{`"ships`": [1]}','[]','{}')"}
 $field.SetValue($null,"Server=127.0.0.1;Database=$dbName;User ID=root;SslMode=None")
 $catalog=$type.GetField('Catalog').GetValue($null)
 foreach($d in $catalog.Values|Where-Object {$_.kind -eq 'item'}){$null=Query "INSERT IGNORE INTO inventory_item_definitions VALUES($($d.value),40,1)"}
 $null=$advance.Invoke($null,@([DateTime]::UtcNow))
 if((Query 'SELECT COUNT(*) FROM auction_lots') -ne $catalog.Count){throw 'Catalog did not seed all auction cycles'}
 $lot=[long](Query "SELECT id FROM auction_lots WHERE item_key='shop_28'")
 if((Request 1 $lot 9999).ok){throw 'Below-minimum bid accepted'}
 if((Credits 1)-ne1000000){throw 'Invalid bid debited credits'}
 $key=[Guid]::NewGuid().ToString('N')
 if(!(Request 1 $lot 10000 $key).ok){throw 'First bid rejected'}
 if(!(Request 1 $lot 10000 $key).ok -or (Credits 1)-ne990000){throw 'Retry duplicated a charge'}
 if(!(Request 1 $lot 20000).ok -or (Credits 1)-ne980000){throw 'Own-bid raise must charge only the difference'}
 if((Request 2 $lot 25000).ok){throw 'Small increment accepted'}
 if(!(Request 2 $lot 30000).ok -or (Credits 1)-ne1000000 -or (Credits 2)-ne970000){throw 'Outbid refund incorrect'}
 if((Request 1 $lot 2000000).ok -or (Credits 1)-ne1000000){throw 'Insufficient funds accepted'}
 $null=Query "UPDATE auction_lots SET ends=UNIX_TIMESTAMP()-1 WHERE id=$lot"
 $null=$advance.Invoke($null,@([DateTime]::UtcNow));$null=$advance.Invoke($null,@([DateTime]::UtcNow))
 if((Query 'SELECT COUNT(*) FROM player_inventory_items WHERE userId=2 AND item_id=7')-ne1){throw 'Winner did not receive exactly one LF3'}
 if((Query "SELECT status FROM auction_lots WHERE id=$lot")-ne'closed'){throw 'Lot was not settled'}
 if((Request 1 $lot 40000).ok){throw 'Ended auction accepted a bid'}
 # Buy does not replace a leading bid or modify the auction result.
 $ammo=[long](Query "SELECT id FROM auction_lots WHERE item_key='shop_2'")
 $buyKey=[Guid]::NewGuid().ToString('N');if(!(Request 1 $ammo 0 $buyKey $true).ok){throw 'Instant ammo buy failed'}
 $null=Request 1 $ammo 0 $buyKey $true
 if((ConvertFrom-Json (Query 'SELECT ammo FROM player_accounts WHERE userId=1')).mcb25-ne1000){throw 'Instant buy duplicated ammo'}
 # Buying/acquiring a ship after bidding causes a refund rather than a lost prize.
 $ship=[long](Query "SELECT id FROM auction_lots WHERE item_key='shop_22'")
 if(!(Request 1 $ship 10000).ok){throw 'Ship bid failed'}
 $null=Query 'UPDATE player_equipment SET items='' {"ships":[1,8]}'' WHERE userId=1'
 $null=Query "UPDATE auction_lots SET ends=UNIX_TIMESTAMP()-1 WHERE id=$ship"
 $null=$advance.Invoke($null,@([DateTime]::UtcNow))
 if((Query "SELECT status FROM auction_lots WHERE id=$ship")-ne'refunded' -or (Credits 1)-ne1000000){throw 'Ineligible ship prize was not refunded'}
 # Every reward path writes the existing inventory format, not a second inventory.
 foreach($key in @('shop_0','shop_25','shop_80','shop_100','ship_49')){
  $target=[long](Query "SELECT id FROM auction_lots WHERE item_key='$key'")
  $result=Request 2 $target 0 ([Guid]::NewGuid().ToString('N')) $true
  if(!$result.ok){throw "Reward kind failed ($key): $($result.message)"}
 }
 $items=ConvertFrom-Json (Query 'SELECT items FROM player_equipment WHERE userId=2')
 if($items.skillTree.logdisks-ne10 -or $items.ships-notcontains49 -or $items.designs.'8'-notcontains18){throw 'Logs, ship or design ownership format incorrect'}
 $drones=ConvertFrom-Json (Query 'SELECT drone FROM player_equipment WHERE userId=2');if(@($drones).Count-ne1 -or $drones[0].Type-ne2){throw 'Iris grant incorrect'}
 $boosts=ConvertFrom-Json (Query 'SELECT boosters FROM player_equipment WHERE userId=2');$seconds=@($boosts.PSObject.Properties.Value|ForEach-Object {$_}|ForEach-Object {$_.Seconds})
 if($seconds-notcontains36000){throw 'Ten-hour booster grant incorrect'}
 $target=[long](Query "SELECT id FROM auction_lots WHERE item_key='shop_100'")
 if((Request 2 $target 0 ([Guid]::NewGuid().ToString('N')) $true).ok){throw 'Duplicate design purchase accepted'}
 $target=[long](Query "SELECT id FROM auction_lots WHERE item_key='shop_2'")
 $null=Query 'UPDATE player_accounts SET ammo=''{"mcb25":2147483647}'' WHERE userId=1'
 if((Request 1 $target 10000).ok -or (Credits 1)-ne1000000){throw 'Overflowing ammo balance accepted'}
 # A live player's balance is authoritative even when its last DB save is older.
 $player=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($asm.GetType('Ow.Game.Objects.Player'))
 $asm.GetType('Ow.Game.Objects.Attackable').GetField('<Id>k__BackingField',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($player,1)
 $player.Data=[Activator]::CreateInstance($player.GetType().GetProperty('Data').PropertyType);$player.Data.credits=500000
 $session=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($asm.GetType('Ow.Game.GameSession'));$session.Player=$player
 $sessions=$asm.GetType('Ow.Managers.GameManager').GetField('GameSessions').GetValue($null)
 $null=$sessions.GetType().GetMethod('TryAdd').Invoke($sessions,@([int]1,$session))
 $target=[long](Query "SELECT id FROM auction_lots WHERE item_key='shop_40'")
 if(!(Request 1 $target 10000).ok -or $player.Data.credits-ne490000 -or (Credits 1)-ne490000){throw 'Live-player debit did not use current balance'}
 if(!(Request 2 $target 20000).ok -or $player.Data.credits-ne500000 -or (Credits 1)-ne500000){throw 'Live-player refund failed'}
 # Concurrent bids must conserve credits: only the leading escrow remains held.
 Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Threading.Tasks;
public static class ConcurrentAuctionProbe {
 public static void CheckRead(Assembly assembly, object command, object parameters) {
  var sync=assembly.GetType("Ow.Game.Events.AuctionManager").GetField("Sync").GetValue(null);
  var execute=assembly.GetType("SocketServer").GetMethod("Execute");
  var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();
  using(var client=new System.Net.Sockets.TcpClient()) {
   client.Connect((System.Net.IPEndPoint)listener.LocalEndpoint);
   var handler=listener.AcceptSocket();Task task;bool completed;
   lock(sync){task=Task.Factory.StartNew(()=>execute.Invoke(null,new[]{command,parameters,(object)handler}));completed=task.Wait(1000);}
   listener.Stop();if(!completed){task.Wait(2000);throw new Exception("Read RPC waited for economic lock");}
   client.ReceiveTimeout=2000;var bytes=new byte[16];var count=client.GetStream().Read(bytes,0,bytes.Length);
   if(count<1)throw new Exception("Read RPC returned no response");
  }
 }
 public static void Run(Type service,long lot) {
  var method=service.GetMethod("Request");var tasks=new Task[20];
  for(int n=0;n<20;n++){int i=n+1;tasks[n]=Task.Factory.StartNew(()=>method.Invoke(null,new object[]{i%2+1,lot,(long)i*10000,Guid.NewGuid().ToString("N"),false}));}
  Task.WaitAll(tasks);
 }
}
'@
 $before=(Credits 1)+(Credits 2);$held=[long](Query "SELECT top_bid FROM auction_lots WHERE id=$target")
 [ConcurrentAuctionProbe]::Run($type,$target)
 if((Query "SELECT top_bid FROM auction_lots WHERE id=$target")-ne200000 -or ((Credits 1)+(Credits 2))-ne($before+$held-200000) -or $player.Data.credits-ne(Credits 1)){throw 'Concurrent bids lost or duplicated credits'}
 $argsRemove=@([int]1,$null);$null=$sessions.GetType().GetMethod('TryRemove').Invoke($sessions,$argsRemove)
 [ConcurrentAuctionProbe]::CheckRead($asm,[Newtonsoft.Json.Linq.JObject]::Parse('{"Action":"OnlineCount"}'),[Newtonsoft.Json.Linq.JObject]::Parse('{}'))
 $bounds=$type.GetMethod('Bounds');$utc=[DateTime]::SpecifyKind([DateTime]'2026-11-02 03:00:00',[DateTimeKind]::Utc)
 $b=$bounds.Invoke($null,@('weekly',$utc));if($b[1]-$b[0]-ne604800 -or $b[0]-ne([DateTimeOffset]$utc).ToUnixTimeSeconds()){throw 'Weekly Bahia boundary incorrect'}
 Write-Output 'Auction passed: cycles, invalid bids, self-raise delta, immediate refund, idempotency, funds, exact-once delivery, all prize kinds, duplicate/overflow rejection, live balance/refund, 20 concurrent bids conserve credits, Monday calendar.'
}finally{
 $field.SetValue($null,$original)
 if($dbName-match'^do_auction_test_[0-9a-f]{32}$'){$null=Query "DROP DATABASE IF EXISTS $dbName"}
 $connection.Dispose()
}
