param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\DarkOrbit.exe'))
$ErrorActionPreference='Stop'
[Environment]::CurrentDirectory=(Split-Path $PSScriptRoot -Parent)
. (Join-Path $PSScriptRoot 'Validate-TutorialRange.ps1') -AssemblyPath $AssemblyPath
$catalog=$asm.GetType('Ow.Game.Objects.Players.QuestCatalog');$definition=$catalog.GetMethod('Get').Invoke($null,@([int]1));$condition=$definition.Conditions[0]
$questsType=$asm.GetType('Ow.Game.Objects.Players.Quests');$quests=[Activator]::CreateInstance($questsType,@($player))
$state=[Activator]::CreateInstance($asm.GetType('Ow.Game.Objects.Players.QuestPlayerState'),@([int]1));$state.State=1
$current=$questsType.GetMethod('Current',[Reflection.BindingFlags]'Instance,NonPublic')
$map.Id=1;$player.FactionId=1
$stations=[Activator]::CreateInstance($map.GetType().GetField('Activatables').FieldType);$map.GetType().GetField('Activatables').SetValue($map,$stations)
$station=Empty 'Ow.Game.Objects.Stations.QuestGiverStation';$asm.GetType('Ow.Game.Objects.Attackable').GetField('<Id>k__BackingField',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($station,3)
$station.Spacemap=$map;$station.FactionId=1;$station.Position=[Activator]::CreateInstance($positionType,@([int]1500,[int]0))
$null=$stations.GetType().GetMethod('TryAdd').Invoke($stations,@([int]3,$station))
if($current.Invoke($quests,@($condition,$state)) -ne 0){throw 'Station objective must not complete from far away'}
$station.Position=[Activator]::CreateInstance($positionType,@([int]500,[int]0))
if($current.Invoke($quests,@($condition,$state)) -ne 1){throw 'Own mission-control station must complete objective'}
$station.FactionId=2
if($current.Invoke($quests,@($condition,$state)) -ne 0){throw 'Wrong faction must not complete objective'}
$station.FactionId=1;$map.GetType().GetProperty('VisualMapId').SetValue($map,255,$null)
if($current.Invoke($quests,@($condition,$state)) -ne 0){throw 'Tutorial map must not complete home station objective'}
$rows=New-Object System.Data.DataTable
$null=$rows.Columns.Add('quest_id',[int]);$null=$rows.Columns.Add('state',[int]);$null=$rows.Columns.Add('progress_json',[string]);$row=$rows.NewRow();$row.quest_id=1;$row.state=2;$row.progress_json='{"join_clan":1}';$rows.Rows.Add($row)
$questsType.GetMethod('LoadPersistedState').Invoke($quests,([object[]](,$rows.PSObject.BaseObject)))
$getState=$questsType.GetMethod('GetState',[Reflection.BindingFlags]'Instance,NonPublic')
$migrated=$getState.Invoke($quests,@([int]1));if($migrated.State -ne 0 -or $migrated.Progress.Count -ne 0){throw 'Old clan quest migration failed'}
$row.progress_json='{"visit_mission_control":1}';$questsType.GetMethod('LoadPersistedState').Invoke($quests,([object[]](,$rows.PSObject.BaseObject)));$completed=$getState.Invoke($quests,@([int]1));if($completed.State -ne 2){throw 'New completed starter mission must remain completed'}
Write-Output 'Starter objective passed: distance, faction, map, clan migration and completed-state preservation'
