param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/DarkOrbit.exe")
$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
function Empty([string]$name){ [Runtime.Serialization.FormatterServices]::GetUninitializedObject($assembly.GetType($name)) }
$p=Empty 'Ow.Game.Objects.Player'
$p.Settings=[Activator]::CreateInstance($assembly.GetType('Ow.Game.Objects.Players.Managers.SettingsBase'))
$p.SkillTree=[Activator]::CreateInstance($assembly.GetType('Ow.Game.Objects.Players.Managers.SkillTreeBase'))
$p.CurrentConfig=1
$p.Ship=Empty 'Ow.Game.Ship'
$p.Equipment=Empty 'Ow.Game.Objects.Players.Managers.EquipmentBase'
$p.Equipment.Configs=Empty 'Ow.Game.Objects.Players.Managers.ConfigsBase'
$p.SettingsManager=Empty 'Ow.Game.Objects.Players.Managers.SettingsManager'
$assembly.GetType('Ow.Game.Objects.Players.Managers.AbstractManager').GetField('<Player>k__BackingField',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($p.SettingsManager,$p)
$p.BoosterManager=[Activator]::CreateInstance($assembly.GetType('Ow.Game.Objects.Players.Managers.BoosterManager'),[object[]]@($p))
function Check([int]$ship,[int]$base,[bool]$friendlyMap,[int]$expected){
 $p.Ship.Id=$ship;$p.Ship.BaseHitpoints=$base;$p.FriendlyMap=$friendlyMap
 $p.Equipment.Configs.Config1Hitpoints=$base;$p.Equipment.Configs.Config2Hitpoints=$base
 foreach($config in @(1,2)){ $p.CurrentConfig=$config; if($p.MaxHitPoints -ne $expected){throw "Ship $ship config $config HP $($p.MaxHitPoints), expected $expected"} }
}
Check 1 4000 $true 4000
Check 5 16000 $true 16000
Check 5 16000 $false 16000
Check 3 64000 $false 64000
Check 3 64000 $true 160000
$p.SkillTree.shiphull1=1
Check 5 16000 $true 21000
$p.SkillTree.shiphull1=0
Check 5 23000 $true 23000
'HP regression passed: base health, both configs, Leonov home-only bonus and single hull bonus.'
