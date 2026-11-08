param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/DarkOrbit.exe")
$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$m=$a.GetType('Ow.Game.Events.AchievementManager').GetMethod('CanReport',[Reflection.BindingFlags]'Static,NonPublic')
function Check($eventName,$map,$expected){
 $actual=$m.Invoke($null,[object[]]@([string]$eventName,[int]$map))
 if($actual -ne $expected){throw "Unexpected achievement eligibility: $eventName / $map"}
}
Check 'fly' 255 $false
foreach($map in @(1,5,9)){Check 'fly' $map $true}
Check 'killNpc' 255 $true
Check 'ore' 255 $true
'Tutorial achievements passed: no first-flight Liberator in tutorial; normal rewards remain enabled.'
