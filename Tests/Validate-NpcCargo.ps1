param([string]$AssemblyPath = 'H:/Darkorbit-Cpp/build/server-npc-cargo/DarkOrbit.exe')
$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$cargoType=$assembly.GetType('Ow.Game.Cargo');$dropType=$assembly.GetType('Ow.Game.Objects.Collectables.CargoDrop')
function Cargo($ores) {$c=[Activator]::CreateInstance($cargoType);foreach($property in $ores.PSObject.Properties){$cargoType.GetProperty($property.Name).SetValue($c,[int]$property.Value,$null)};return $c}
function Drop($cargo,[double]$multiplier=1) {return $dropType.GetConstructors()[0].Invoke([object[]]@($cargo,$multiplier))}
function Ensure([bool]$ok,[string]$reason){if(!$ok){throw $reason}}
$manifest=Get-Content "$PSScriptRoot/../config/reference/npc-cargo-wiki-20261103.json" -Raw | ConvertFrom-Json
$state=@{free=100000;received=@{}}
$grant=[Func[Ow.Game.Ores,int,int]]{param($ore,$amount) $applied=[Math]::Min($amount,$state.free);$state.free-=$applied;$state.received[$ore.ToString()]+=$applied;return $applied}
foreach($row in $manifest.rows){
 $state.free=100000;$state.received=@{};$drop=Drop (Cargo $row.ores)
 $total=$drop.Collect(0,$grant)
 foreach($ore in $row.ores.PSObject.Properties){Ensure ([int]$state.received[$ore.Name] -eq [int]$ore.Value) "Incorrect $($row.name) $($ore.Name)"}
 Ensure $drop.Empty "Box not empty after full pickup: $($row.name)"
 Ensure ($drop.Collect(0,$grant) -eq 0) 'Cargo can be awarded twice'
}
$source=Cargo ([pscustomobject]@{Prometium=100;Xenomit=2;Seprom=1})
$drop=Drop $source;$source.Prometium=9999
$state.free=1;$state.received=@{};Ensure ($drop.Collect(0,$grant) -eq 1) 'Partial pickup amount'
Ensure ([int]$state.received.Seprom -eq 1) 'Rare ore not prioritized'
Ensure (!$drop.Empty) 'Partial pickup erased remaining resources'
$state.free=1000;Ensure ($drop.Collect(0,$grant) -eq 102) 'Box changed after source/NPC respawn mutation'
Ensure $drop.Empty 'Partial pickup never completes'
$drop=Drop (Cargo ([pscustomobject]@{Prometium=100}));$state.free=3;$state.received=@{}
Ensure ($drop.Collect(6,$grant) -eq 3) 'Cargo skill partial pickup'
$state.free=1000;Ensure ($drop.Collect(6,$grant) -eq 103) 'Cargo skill applied again on remaining contents'
Ensure ([int]$state.received.Prometium -eq 106) 'Wrong Tractor Beam I bonus'
$drop=Drop (Cargo ([pscustomobject]@{Prometium=10;Endurium=10})) 3
$state.free=1000;$state.received=@{};Ensure ($drop.Collect(0,$grant) -eq 60) 'Galaxy gate instance multiplier'
$empty=Drop $null;Ensure $empty.Empty 'Missing drop data generated test resources'
$drop=Drop (Cargo ([pscustomobject]@{Prometium=100}));$state.free=0;$state.received=@{}
Ensure ($drop.Collect(0,$grant) -eq 0 -and !$drop.Empty) 'Full cargo destroyed or duplicated the drop'
"NPC cargo: $($manifest.rows.Count) official tables, exact grant, immutable snapshot, instance multiplier, partial/full cargo, rare-ore priority and single-use skill passed."
