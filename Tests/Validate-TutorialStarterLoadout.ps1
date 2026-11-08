param([string]$AssemblyPath = 'H:\Darkorbit-Cpp\build\server-quest-framing\DarkOrbit.exe', [string]$WindowDefaultsPath = '')
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$null = [Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $AssemblyPath) 'MySql.Data.dll'))
$null = [Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $AssemblyPath) 'Newtonsoft.Json.dll'))
$connection = New-Object MySql.Data.MySqlClient.MySqlConnection 'Server=127.0.0.1;Database=darkorbit;User ID=root;SslMode=None'
$connection.Open()
function Query([string]$sql) { $command=$connection.CreateCommand(); $command.CommandText=$sql; try { return $command.ExecuteScalar() } finally { $command.Dispose() } }
try {
    # These tables shadow the real tables only on this connection; no player data changes.
    $null=Query 'CREATE TEMPORARY TABLE player_accounts (userId INT PRIMARY KEY) ENGINE=InnoDB'
    foreach ($table in @('player_inventory_items','player_inventory_loadout')) {
        $command=$connection.CreateCommand();$command.CommandText="SHOW CREATE TABLE $table"
        $reader=$command.ExecuteReader()
        try { $null=$reader.Read();$ddl=$reader.GetString(1).Replace('CREATE TABLE','CREATE TEMPORARY TABLE') }
        finally { $reader.Dispose();$command.Dispose() }
        $null=Query $ddl
    }
    $null=Query 'INSERT INTO player_accounts (userId) VALUES (987654321)'
    $clientType=$assembly.GetType('Ow.Managers.MySQLManager.SqlDatabaseClient')
    $client=$clientType.GetConstructors()[0].Invoke([object[]](,$connection.PSObject.BaseObject))
    $method=$assembly.GetType('Ow.Managers.QueryManager').GetMethod('EnsureTutorialStarterLoadout',[Reflection.BindingFlags]'Static,NonPublic')
    $null=$method.Invoke($null,@($client,[int]987654321));$null=$method.Invoke($null,@($client,[int]987654321))
    if ((Query 'SELECT COUNT(*) FROM player_inventory_items') -ne 1) { throw 'Starter laser duplicated' }
    if ((Query "SELECT COUNT(*) FROM player_inventory_loadout WHERE item_id=124 AND mode='ship' AND slot_group='lasers' AND config_id IN (1,2)") -ne 2) { throw 'Starter MP-1 must be equipped in both configs' }
    $null=Query 'UPDATE player_inventory_items SET item_id=7';$null=Query 'UPDATE player_inventory_loadout SET item_id=7'
    $null=$method.Invoke($null,@($client,[int]987654321))
    if ((Query 'SELECT COUNT(*) FROM player_inventory_loadout WHERE item_id=7') -ne 2) { throw 'Existing equipment was overwritten' }
    $null=Query 'DELETE FROM player_inventory_loadout WHERE config_id=2'
    $null=$method.Invoke($null,@($client,[int]987654321))
    if ((Query 'SELECT COUNT(*) FROM player_inventory_items') -ne 1 -or (Query 'SELECT COUNT(*) FROM player_inventory_loadout WHERE item_id=7') -ne 2) { throw 'Owned laser must be reused for missing config' }
    $settings=[Activator]::CreateInstance($assembly.GetType('Ow.Game.Objects.Players.Managers.SettingsBase'))
    if ($settings.SlotBarItems[[int16]1] -ne 'ammunition_laser_lcb-10') { throw 'Default hotbar must contain X1' }
    if ($settings.SlotBarItems[[int16]2] -ne 'ammunition_rocket_r-310') { throw 'Default hotbar must contain R-310 in slot 2' }
    if ($settings.SlotBarItems.Count -ne 3 -or $settings.SlotBarItems[[int16]3] -ne 'equipment_extra_repbot_rep-s' -or $settings.PremiumSlotBarItems.Count -ne 0 -or $settings.ProActionBarItems.Count -ne 0) { throw 'Only LCB-10, R-310 and REP-S belong on the starter hotbar' }
    $open=@($settings.Window.windows.GetEnumerator() | Where-Object { $_.Value.maximixed } | ForEach-Object { $_.Key } | Sort-Object)
    if (($open -join ',') -ne 'minimap,ship,target,user') { throw 'Unexpected initial open windows' }
    if ($settings.Window.windows['ship'].x -ne 15 -or $settings.Window.windows['user'].x -ne 1000001 -or $settings.Window.windows['minimap'].y -ne 1000001 -or $settings.Window.windows['target'].x -ne 1000002) { throw 'Initial window anchors are incorrect' }
    if ($WindowDefaultsPath) { [IO.File]::WriteAllText($WindowDefaultsPath, [Newtonsoft.Json.JsonConvert]::SerializeObject($settings.Window)) }
    $player=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($assembly.GetType('Ow.Game.Objects.Player'))
    $player.Settings=$settings;$player.Ammo=[Activator]::CreateInstance($assembly.GetType('Ow.Game.Ammo'))
    $settings.SlotBarItems.Clear();$settings.SlotBarItems.Add([int16]1,'ammunition_laser_ucb-100')
    $settings.InGameSettings.selectedRocket='ammunition_rocket_plt-3030';$player.Ammo.plt3030=50
    $supplies=$assembly.GetType('Ow.Managers.QueryManager').GetMethod('ConfigureTutorialStarterSupplies',[Reflection.BindingFlags]'Static,NonPublic')
    $null=$supplies.Invoke($null,@($player))
    if ($player.Ammo.r310 -ne 50 -or $player.Ammo.plt3030 -ne 0 -or $settings.InGameSettings.selectedRocket -ne 'ammunition_rocket_r-310') { throw 'Starter rockets must convert to R-310 and become selected' }
    if ($settings.SlotBarItems[[int16]1] -ne 'ammunition_laser_lcb-10' -or $settings.SlotBarItems[[int16]2] -ne 'ammunition_rocket_r-310') { throw 'Migrated hotbar must contain X1 and R-310' }
    if ($supplies.Invoke($null,@($player))) { throw 'Supplies migration must be idempotent' }
    $player.Ammo.r310=0;$null=$supplies.Invoke($null,@($player))
    if ($player.Ammo.r310 -ne 0) { throw 'Consumed rockets must not refill on login' }
    $repair=$assembly.GetType('Ow.Managers.QueryManager').GetMethod('EnsureStarterRepairBot',[Reflection.BindingFlags]'Static,NonPublic')
    $null=$repair.Invoke($null,@($client,[int]987654321));$null=$repair.Invoke($null,@($client,[int]987654321))
    if((Query 'SELECT COUNT(*) FROM player_inventory_items WHERE item_id=43') -ne 1){throw 'Default REP-S must not duplicate'}
    if((Query "SELECT COUNT(*) FROM player_inventory_loadout WHERE item_id=43 AND config_id IN (1,2) AND slot_group='cpus'") -ne 2){throw 'REP-S must be equipped in both configs'}
    $null=Query 'UPDATE player_inventory_items SET item_id=47 WHERE item_id=43';$null=Query 'UPDATE player_inventory_loadout SET item_id=47 WHERE item_id=43'
    $null=$repair.Invoke($null,@($client,[int]987654321))
    if((Query 'SELECT COUNT(*) FROM player_inventory_items WHERE item_id=43') -ne 0 -or (Query 'SELECT COUNT(*) FROM player_inventory_loadout WHERE item_id=47') -ne 2){throw 'Better repair robot was replaced'}
    Write-Output 'Tutorial starter passed: MP-1 loadout, existing gear preserved, X1/R-310 hotbar, rocket conversion/selection and no login refill'
} finally { $connection.Dispose() }
