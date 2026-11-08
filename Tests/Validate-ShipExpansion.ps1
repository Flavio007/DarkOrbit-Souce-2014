param([string]$AssemblyPath='H:\Darkorbit-Cpp\build\server-events\DarkOrbit.exe')
$ErrorActionPreference='Stop';$asm=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$ship=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($asm.GetType('Ow.Game.Ship',$true));$ship.Id=5;$ship.LaserSlots=4
$type=$asm.GetType('Ow.Game.PlayerShipExtension',$true);$resolve=$type.GetMethod('ResolveLevel')
foreach($case in @(@(0,$false,$false,1),@(1,$false,$false,1),@(2,$true,$false,2),@(1,$false,$true,2),@(3,$false,$true,2),@(4,$false,$true,3),@(4,$true,$false,2),@(4,$false,$false,1))){$got=$resolve.Invoke($null,@($ship,[int]$case[0],[bool]$case[1],[bool]$case[2]));if($got -ne $case[3]){throw "Expansion mismatch: expected $($case[3]), got $got"}}
if($type.GetMethod('GetDefaultLevel').Invoke($null,@($ship)) -ne 1){throw 'Classic ships must start at stage 1'}
$ship.Id=20;if($type.GetMethod('GetDefaultLevel').Invoke($null,@($ship)) -ne 3){throw 'Special ship fallback changed'}
Write-Output 'Expansion passed: mixed LF-1/LF-2, partial LF-3, full LF-3, full basic lasers and special ships.'
