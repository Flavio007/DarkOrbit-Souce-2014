param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/DarkOrbit.exe")
$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$p=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($a.GetType('Ow.Game.Objects.Player'))
$p.Storage=[Activator]::CreateInstance($a.GetType('Ow.Game.Objects.Storage'),[object[]]@($p))
$p.Storage.CitadelFortify=$true
if($p.Speed -ne 0){throw 'Fortify must force zero speed before other modifiers'}
$p.Moving=$true
$position=[Activator]::CreateInstance($a.GetType('Ow.Game.Movements.Position'),[object[]]@(999,999))
$a.GetType('Ow.Game.Movements.Movement').GetMethod('Move').Invoke($null,[object[]]@($p,$position))
if($p.Moving){throw 'Movement must reject destinations while fortified'}
$origin=[Activator]::CreateInstance($a.GetType('Ow.Game.Movements.Position'),[object[]]@(0,0))
$p.Position=$origin;$p.OldPosition=$origin;$p.Destination=$position;$p.Direction=$position
$p.MovementTime=10000;$p.MovementStartTime=[DateTime]::Now.AddSeconds(-5);$p.Moving=$true
$stopped=$a.GetType('Ow.Game.Movements.Movement').GetMethod('Stop').Invoke($null,[object[]]@($p))
if($p.Moving -or $p.MovementTime -ne 0){throw 'Fortify stop must cancel the entire movement segment'}
if($stopped.X -lt 450 -or $stopped.X -gt 550){throw 'Fortify stop must preserve the current interpolated position'}
if($p.Destination.X -ne $stopped.X -or $p.Direction.X -ne 0){throw 'Fortify stop must discard the previous destination and direction'}
$a.GetType('Ow.Game.Movements.Movement').GetMethod('Move').Invoke($null,[object[]]@($p,$position))
if($p.Moving -or $p.Destination.X -ne $stopped.X){throw 'A late move must not undo the Fortify stop'}
'Fortify regression passed: zero speed, immediate midpoint stop, stale movement rejection.' 
