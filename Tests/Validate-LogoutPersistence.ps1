param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/DarkOrbit.exe")
$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$player=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($assembly.GetType('Ow.Game.Objects.Player'))
$player.Premium=$false; $player.RankId=1
if($player.LogoutDuration -ne 20){throw 'Normal logout must take 20 seconds'}
$player.Premium=$true
if($player.LogoutDuration -ne 5){throw 'Premium logout must take 5 seconds'}
$player.Premium=$false; $player.RankId=21
if($player.LogoutDuration -ne 5){throw 'Admin logout must take 5 seconds'}
$sessionType=$assembly.GetType('Ow.Game.GameSession')
$session=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($sessionType)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$sessionType.GetField('persistenceSync',$flags).SetValue($session,(New-Object object))
$sessionType.GetField('disconnected',$flags).SetValue($session,$true)
if(-not $session.SaveProgress()){throw 'A disconnected session must not write stale state'}
'Logout persistence regression passed: Flash durations and retired session save guard.'
