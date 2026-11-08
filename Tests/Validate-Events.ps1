param([string]$AssemblyPath='H:\Darkorbit-Cpp\build\server-events\DarkOrbit.exe')
$ErrorActionPreference='Stop'
$asm=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$type=$asm.GetType('Ow.Game.Events.ScoremageddonState',$true)
$state=[Activator]::CreateInstance($type,$true)
$now=[DateTime]::UtcNow
if($state.Lives -ne 5 -or $state.Points -ne 0){throw 'Invalid initial score state'}
$state.Kill($now);$state.Kill($now.AddSeconds(1))
if($state.Combo -ne 2 -or $state.Points -ne 300){throw 'Kill combo/points incorrect'}
if($state.RemainingPercent($now.AddSeconds(31)) -ne 50){throw 'Combo countdown incorrect'}
$state.Kill($now.AddSeconds(62))
if($state.Combo -ne 1 -or $state.Points -ne 400){throw 'Expired combo did not restart'}
1..20|ForEach-Object{$state.Kill($now.AddSeconds(63))}
if($state.Combo -ne 10){throw 'Combo exceeds limit'}
1..10|ForEach-Object{$state.Die()}
if($state.Lives -ne 0 -or $state.Combo -ne 0 -or $state.RemainingPercent($now) -ne 0){throw 'Life floor/reset failed'}
$ball=$asm.GetType('Ow.Game.Objects.Spaceball',$true).GetMethod('SpeedTier')
foreach($case in @(@(0,1),@(499999,1),@(500000,2),@(999999,2),@(1000000,3),@([long]2147483648,3))){if($ball.Invoke($null,@([long]$case[0])) -ne $case[1]){throw 'Spaceball threshold failed'}}
Write-Output 'Events passed: score lives, points, combo limit/expiry/countdown; Spaceball speed thresholds and large damage.'
