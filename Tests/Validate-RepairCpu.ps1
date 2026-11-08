param([string]$AssemblyPath='H:\Darkorbit-Cpp\build\server-events\DarkOrbit.exe')
$ErrorActionPreference='Stop';$asm=[Reflection.Assembly]::LoadFrom($AssemblyPath)
function Empty($n){[Runtime.Serialization.FormatterServices]::GetUninitializedObject($asm.GetType($n,$true))}
$p=Empty 'Ow.Game.Objects.Player';$p.Settings=[Activator]::CreateInstance($asm.GetType('Ow.Game.Objects.Players.Managers.SettingsBase'));$p.Storage=Empty 'Ow.Game.Objects.Storage';$p.Ammo=[Activator]::CreateInstance($asm.GetType('Ow.Game.Ammo'))
$cpu=Empty 'Ow.Game.Objects.Players.Managers.CpuManager';$settings=Empty 'Ow.Game.Objects.Players.Managers.SettingsManager'
$flags=[Reflection.BindingFlags]'Instance,NonPublic';$base=$asm.GetType('Ow.Game.Objects.Players.Managers.AbstractManager');$base.GetField('<Player>k__BackingField',$flags).SetValue($cpu,$p);$base.GetField('<Player>k__BackingField',$flags).SetValue($settings,$p);$p.CpuManager=$cpu;$p.SettingsManager=$settings
$equipped=New-Object 'System.Collections.Generic.HashSet[string]';$null=$equipped.Add('equipment_extra_cpu_nc-rrb');$null=$equipped.Add('equipment_extra_repbot_rep-s');$cpu.GetType().GetField('equippedCpus',$flags).SetValue($cpu,$equipped);$cpu.GetType().GetField('enforceEquippedCpuValidation',$flags).SetValue($cpu,$true)
foreach($f in $cpu.GetType().GetFields($flags)){if($f.FieldType.IsGenericType -and $null -eq $f.GetValue($cpu)){ $f.SetValue($cpu,[Activator]::CreateInstance($f.FieldType)) }}
$id='equipment_extra_cpu_nc-rrb'
if($cpu.CanUseRepairBot()){throw 'Automatic repair active while CPU off'}
$cpu.ToggleAutomaticRepair()
if(!$p.Settings.InGameSettings.selectedCpus.Contains($id) -or !$cpu.CanUseRepairBot()){throw 'CPU did not enable'}
$status=$settings.GetItemStatus($id,'ttip',0,$true,$false,$false,$true,$false)
if(!$status.selected){throw 'CPU enabled highlight missing'}
$cpu.ToggleAutomaticRepair()
if($p.Settings.InGameSettings.selectedCpus.Contains($id) -or $cpu.CanUseRepairBot()){throw 'CPU did not disable'}
$p.Storage.RepairBotActivated=$true;if(!$cpu.CanUseRepairBot()){throw 'Manual repair blocked'}
Write-Output 'Repair CPU passed: off/on/off through CPU command, selected highlight and manual repair preserved.'

