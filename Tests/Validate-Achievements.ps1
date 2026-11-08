param([string]$AssemblyPath='H:\Darkorbit-Cpp\build\server-achievements\DarkOrbit.exe')
$ErrorActionPreference='Stop'
[Environment]::CurrentDirectory=(Split-Path $PSScriptRoot -Parent)
$asm=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$null=[Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $AssemblyPath) 'MySql.Data.dll'))
$dbName='do_achievement_test_'+[Guid]::NewGuid().ToString('N')
$connection=New-Object MySql.Data.MySqlClient.MySqlConnection 'Server=127.0.0.1;User ID=root;SslMode=None'
$connection.Open()
function Query([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandText=$sql;try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Empty([string]$name){return [Runtime.Serialization.FormatterServices]::GetUninitializedObject($asm.GetType($name,$true))}
$database=$asm.GetType('Ow.Managers.MySQLManager.SqlDatabaseManager');$field=$database.GetField('ConnectionString');$original=$field.GetValue($null)
try{
    $null=Query "CREATE DATABASE $dbName";$connection.ChangeDatabase($dbName)
    $null=Query 'CREATE TABLE player_accounts (userId INT PRIMARY KEY,achievement LONGTEXT,data LONGTEXT,ammo LONGTEXT,cargo LONGTEXT,repairCredits BIGINT DEFAULT 0) ENGINE=InnoDB'
    $null=Query 'CREATE TABLE player_equipment (userId INT PRIMARY KEY,items LONGTEXT,drone LONGTEXT) ENGINE=InnoDB'
    $null=Query 'CREATE TABLE player_inventory_items (id BIGINT AUTO_INCREMENT PRIMARY KEY,userId INT,item_id INT) ENGINE=InnoDB'
    $null=Query "INSERT INTO player_accounts VALUES (987654321,'[]','{}','{}','{}',0)"
    $null=Query "INSERT INTO player_equipment VALUES (987654321,'{`"ships`": [1,8]}','[]')"
    $field.SetValue($null,"Server=127.0.0.1;Database=$dbName;User ID=root;SslMode=None")
    $asm.GetType('Ow.Game.Events.AchievementManager').GetMethod('EnsureSchema').Invoke($null,@())
    if((Query 'SELECT achievements FROM player_accounts') -ne '[]'){throw 'Legacy achievement column migration failed'}
    $player=Empty 'Ow.Game.Objects.Player'
    $asm.GetType('Ow.Game.Objects.Attackable').GetField('<Id>k__BackingField',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($player,987654321)
    $player.Data=[Activator]::CreateInstance($player.GetType().GetProperty('Data').PropertyType)
    $player.Ammo=[Activator]::CreateInstance($asm.GetType('Ow.Game.Ammo'))
    $player.DroneManager=Empty 'Ow.Game.Objects.Players.Managers.DroneManager'
    $player.DroneManager.DronesList=[Activator]::CreateInstance($player.DroneManager.GetType().GetField('DronesList').FieldType)
    $type=$asm.GetType('Ow.Game.Events.AchievementManager');$manager=[Activator]::CreateInstance($type,@($player))
    $catalog=$type.GetField('Catalog').GetValue($null);if($catalog.Count -ne 12){throw 'Original twelve achievements missing'}
    $matches=$type.GetMethod('Matches')
    if(!$matches.Invoke($null,@($catalog[2],'killNpc',84)) -or $matches.Invoke($null,@($catalog[2],'killNpc',71)) -or !$matches.Invoke($null,@($catalog[5],'ore',1)) -or $matches.Invoke($null,@($catalog[5],'ore',2))){throw 'Achievement target filter failed'}
    $manager.Load('[{"id":2,"done":1,"bargainState":1}]')
    if((ConvertFrom-Json $manager.Serialize())[0].rewarded -ne $true){throw 'Legacy completed reward migration failed'}
    $all=@(1..12 | ForEach-Object {@{id=$_;done=1;progress=1;rewarded=$false;bargainState=3}})|ConvertTo-Json -Compress
    $manager.Load($all)
    $entries=$type.GetField('entries',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($manager)
    $reward=$type.GetMethod('TryReward',[Reflection.BindingFlags]'Instance,NonPublic')
    foreach($id in 1..12){$null=$reward.Invoke($manager,@($entries[$id]));if(!$entries[$id].rewarded){throw "Reward failed: $id"}}
    if((Query 'SELECT COUNT(*) FROM player_inventory_items') -ne 5){throw 'Equipment reward count incorrect'}
    if($player.Data.uridium -ne 1000 -or $player.Ammo.mcb50 -ne 2500 -or $player.Ammo.plt21 -ne 500 -or $player.Data.repairCredits -ne 5 -or $player.Promerium -ne 100 -or $player.DroneManager.DronesList.Count -ne 1){throw 'Original reward amounts incorrect'}
    $ships=ConvertFrom-Json (Query 'SELECT items FROM player_equipment');if($ships.ships -notcontains 1 -or $ships.ships -notcontains 8 -or $ships.ships -contains 5 -or $player.Data.credits -ne 64000){throw 'Superior owned ship must receive credits without changing ownership'}
    # Simulate a stale reconnect; the committed DB reward markers must win.
    $manager.Load($all);foreach($id in 1..12){$null=$reward.Invoke($manager,@($entries[$id]))}
    if((Query 'SELECT COUNT(*) FROM player_inventory_items') -ne 5 -or $player.Data.uridium -ne 1000 -or $player.Data.credits -ne 64000 -or $player.DroneManager.DronesList.Count -ne 1){throw 'Rewards duplicated on replay'}
    $null=Query 'ALTER TABLE player_accounts ADD shipId INT DEFAULT 1'
    $null=Query "UPDATE player_accounts SET achievements='[]'"
    $null=Query "UPDATE player_equipment SET items='{`"ships`": [1,2,4]}'"
    $manager.Load('[{"id":1,"done":1,"progress":1,"rewarded":false}]')
    $null=$reward.Invoke($manager,@($entries[1]))
    $ships=ConvertFrom-Json (Query 'SELECT items FROM player_equipment')
    if(!$entries[1].rewarded -or $ships.ships -notcontains 5 -or (Query 'SELECT shipId FROM player_accounts') -ne 5 -or $player.Data.credits -ne 64000){throw 'Liberator must be equipped and persisted immediately for starter hulls'}
    $manager.Load('[{"id":2,"done":0,"progress":0,"rewarded":false}]');$manager.HandleBuyRequest(2)
    if($entries[2].done -ne 0){throw 'Client buy packet completed an unearned achievement'}
    $manager.Report('ore',3,2);if($entries[5].progress -ne 0){throw 'Wrong ore counted'}
    $manager.Report('ore',3,1);$json=$manager.Serialize();$manager.Load($json)
    if($entries[5].progress -ne 3 -or $entries[5].done -ne 0){throw 'Partial progress did not survive reload'}
    Write-Output 'Achievements passed: 12 definitions, exact NPC/ore filters, legacy migration, all original rewards, preserved ships, stale-reconnect deduplication, forged completion rejection'
}finally{
    $field.SetValue($null,$original)
    if($dbName -match '^do_achievement_test_[0-9a-f]{32}$'){$null=Query "DROP DATABASE IF EXISTS $dbName"}
    $connection.Dispose()
}
