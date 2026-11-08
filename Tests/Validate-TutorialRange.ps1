param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\DarkOrbit.exe'))
$ErrorActionPreference='Stop'
$asm=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
function Empty([string]$name){return [Runtime.Serialization.FormatterServices]::GetUninitializedObject($asm.GetType($name,$true))}
$map=Empty 'Ow.Game.Spacemap';$map.Id=20000
$map.GetType().GetProperty('VisualMapId').SetValue($map,255,$null)
$map.Options=[Activator]::CreateInstance($map.GetType().GetProperty('Options').PropertyType)
$characters=[Activator]::CreateInstance($map.GetType().GetField('Characters').FieldType)
$map.GetType().GetField('Characters').SetValue($map,$characters)
$player=Empty 'Ow.Game.Objects.Player';$npc=Empty 'Ow.Game.Objects.Npc'
$ids=$asm.GetType('Ow.Game.Objects.Attackable').GetField('<Id>k__BackingField',[Reflection.BindingFlags]'NonPublic,Instance')
$ids.SetValue($player,1);$ids.SetValue($npc,2)
$player.Storage=Empty 'Ow.Game.Objects.Storage'
$positionType=$asm.GetType('Ow.Game.Movements.Position')
$player.Position=[Activator]::CreateInstance($positionType,@([int]0,[int]0));$npc.Position=[Activator]::CreateInstance($positionType,@([int]10000,[int]5000))
$player.Spacemap=$map;$npc.Spacemap=$map
$null=$characters.GetType().GetMethod('TryAdd').Invoke($characters,@([int]1,$player));$null=$characters.GetType().GetMethod('TryAdd').Invoke($characters,@([int]2,$npc))
$canSee=$map.GetType().GetMethod('CanSeeCharacter')
if(!$canSee.Invoke($map,@($player,$npc))){throw 'Tutorial radar must see far NPC'}
if($canSee.Invoke($map,@($npc,$player))){throw 'NPC range must not become map-wide'}
$inRange=$asm.GetType('Ow.Game.Objects.Attackable').GetMethod('InRange')
if($inRange.Invoke($player,@($npc,[int]700))){throw 'Tutorial attack range must remain limited'}
$map.GetType().GetProperty('VisualMapId').SetValue($map,1,$null)
if($canSee.Invoke($map,@($player,$npc))){throw 'Normal map radar must retain range'}
$limits=[Array]::CreateInstance($positionType,2)
$limits.SetValue([Activator]::CreateInstance($positionType,@([int]0,[int]0)),0)
$limits.SetValue([Activator]::CreateInstance($positionType,@([int]10450,[int]6500)),1)
$map.GetType().GetProperty('Limits').SetValue($map,$limits,$null)
$tutorial=Empty 'Ow.Game.Events.TutorialManager'
$spawn=$tutorial.GetType().GetMethod('ResolvePlayerPosition',[Reflection.BindingFlags]'NonPublic,Instance').Invoke($tutorial,@($player,$map))
if($spawn.X -ne 5225 -or $spawn.Y -ne 4500){throw 'Fresh tutorial must start near combat instead of retaining the corner position'}
Write-Output 'Tutorial passed: full radar, normal NPC and attack ranges, normal-map range preserved, combat-adjacent spawn'
