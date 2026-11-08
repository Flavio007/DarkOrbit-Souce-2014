param([string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Validate-TutorialRange.ps1') -AssemblyPath $AssemblyPath
$quest = Empty 'Ow.Game.Objects.Players.Quests'
$quest.Player = $player
$definitionType = $asm.GetType('Ow.Game.Objects.Players.QuestDefinition')
$conditionType = $asm.GetType('Ow.Game.Objects.Players.QuestConditionDefinition')
$stateType = $asm.GetType('Ow.Game.Objects.Players.QuestPlayerState')
function Condition($id, $branch, $target) {
    $c = [Activator]::CreateInstance($conditionType, $true)
    $c.Id = $id; $c.Branch = $branch; $c.Type = 'collectOre'; $c.Target = $target
    return $c
}
$definition = [Activator]::CreateInstance($definitionType, $true)
$definition.Conditions.Add((Condition 'mandatory' '' 1))
$definition.Conditions.Add((Condition 'gather1' 'gather' 5))
$definition.Conditions.Add((Condition 'gather2' 'gather' 5))
$definition.Conditions.Add((Condition 'hunt1' 'hunt' 10))
$definition.Conditions.Add((Condition 'hunt2' 'hunt' 5))
$state = [Activator]::CreateInstance($stateType, @([int]99999))
$state.State = 1
$state.Progress['mandatory'] = 1; $state.Progress['gather1'] = 5; $state.Progress['hunt2'] = 5
$evaluate = $quest.GetType().GetMethod('Evaluate', [Reflection.BindingFlags]'Instance,NonPublic')
$arguments = [object[]]@($definition, $state, $false)
$null = $evaluate.Invoke($quest, $arguments)
if ($state.State -ne 1) { throw 'Partial progress from different branches completed a quest' }
$state.Progress['gather2'] = 5
$arguments = [object[]]@($definition, $state, $false)
$null = $evaluate.Invoke($quest, $arguments)
if ($state.State -ne 2) { throw 'A full branch plus mandatory objectives did not complete' }
$mapCheck = $quest.GetType().GetMethod('MatchesMap', [Reflection.BindingFlags]'Instance,NonPublic')
$condition = Condition 'map' '' 1
$condition.MapRelation = 'enemy'; $condition.MapSector = 7; $condition.MapOwner = 2
$map.Name = '2-7'; $map.FactionId = 2; $player.FactionId = 1
if (!$mapCheck.Invoke($quest, @($condition))) { throw 'Enemy upper-map filter failed' }
$map.Name = '2-6'
if ($mapCheck.Invoke($quest, @($condition))) { throw 'Wrong sector progressed the objective' }
$map.Name = '2-7'; $player.FactionId = 2
if ($mapCheck.Invoke($quest, @($condition))) { throw 'Own company was treated as enemy territory' }
$player.FactionId = 1
$player.Settings = [Activator]::CreateInstance($player.GetType().GetField('Settings').FieldType)
$player.SkillTree = [Activator]::CreateInstance($player.GetType().GetField('SkillTree').FieldType)
$player.Ship = Empty 'Ow.Game.Ship'; $player.Ship.Id = 1
$player.Equipment = Empty 'Ow.Game.Objects.Players.Managers.EquipmentBase'
$player.Equipment.Configs = Empty 'Ow.Game.Objects.Players.Managers.ConfigsBase'
$player.Equipment.Configs.Config1Hitpoints = 1000; $player.Equipment.Configs.Config2Hitpoints = 1000
$player.BoosterManager = [Activator]::CreateInstance($asm.GetType('Ow.Game.Objects.Players.Managers.BoosterManager'), @($player))
$player.SettingsManager = Empty 'Ow.Game.Objects.Players.Managers.SettingsManager'
$asm.GetType('Ow.Game.Objects.Players.Managers.AbstractManager').GetField('<Player>k__BackingField', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($player.SettingsManager, $player)
$player.CurrentHitPoints = 100
$condition.MaxHpPercent = 10
if ($mapCheck.Invoke($quest, @($condition))) { throw 'Exactly 10 percent HP passed a strictly-below restriction' }
$player.CurrentHitPoints = 99
if (!$mapCheck.Invoke($quest, @($condition))) { throw 'Below-10-percent HP restriction failed' }
$condition.RequireCloak = -1; $player.Invisible = $true
if ($mapCheck.Invoke($quest, @($condition))) { throw 'Cloaked ship progressed uncloaked objective' }
$questType = $quest.GetType()
$dailyQuest = $questType.GetConstructors()[0].Invoke([object[]]@($null))
$definitions = $asm.GetType('Ow.Game.Objects.Players.QuestCatalog').GetMethod('All').Invoke($null, @())
$dailyDefinition = $definitions | Where-Object Daily | Select-Object -First 1
$getState = $questType.GetMethod('GetState', [Reflection.BindingFlags]'Instance,NonPublic')
$dailyState = $getState.Invoke($dailyQuest, @([int]$dailyDefinition.Id))
$today = $questType.GetProperty('DailyDate', [Reflection.BindingFlags]'Static,NonPublic').GetValue($null, $null)
$reset = $questType.GetMethod('ResetDailyQuests', [Reflection.BindingFlags]'Instance,NonPublic')
$dailyState.State = 2; $dailyState.Progress['$completedDay'] = $today
$null = $reset.Invoke($dailyQuest, @())
if ($dailyState.State -ne 2) { throw 'Daily quest reopened on the same day' }
$dailyState.Progress['$completedDay'] = $today - 1
$null = $reset.Invoke($dailyQuest, @())
if ($dailyState.State -ne 0 -or $dailyState.Progress.Count -ne 0) { throw 'Daily quest did not reset after the day changed' }
$dailyState.State = 1; $dailyState.Progress['$started'] = 1
$null = $reset.Invoke($dailyQuest, @())
if ($dailyState.State -ne 1) { throw 'Active daily quest was discarded at rollover' }
$player.Data = [Activator]::CreateInstance($player.GetType().GetProperty('Data').PropertyType)
$slotQuest = $questType.GetConstructors()[0].Invoke([object[]]@($player))
foreach ($id in @(1,101,102,1001,1002)) {
    $active = $getState.Invoke($slotQuest, @([int]$id)); $active.State = 1
}
$eligible = $questType.GetMethod('IsEligible', [Reflection.BindingFlags]'Instance,NonPublic')
$normalDefinition = $definitions | Where-Object Id -eq 1003
if ($eligible.Invoke($slotQuest, @($normalDefinition.PSObject.BaseObject))) { throw 'Normal quest exceeded the normal slot limit' }
if (!$eligible.Invoke($slotQuest, @($dailyDefinition.PSObject.BaseObject))) { throw 'Normal slots incorrectly blocked a daily quest' }
$remaining = $questType.GetMethod('GetQuestSlotsRemaining', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($slotQuest, @())
if ($remaining -ne 12) { throw 'Daily capacity is missing from the UI slot count' }
Write-Output 'Classic rules passed: compound alternatives, mandatory steps, enemy maps, HP/cloak restrictions, daily rollover and separate normal/daily capacities.'
