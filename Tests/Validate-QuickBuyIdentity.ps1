param([string]$AssemblyPath='H:\Darkorbit-Cpp\build\server-events\DarkOrbit.exe')
$ErrorActionPreference='Stop'
$asm=[Reflection.Assembly]::LoadFrom($AssemblyPath)
$type=$asm.GetType('Ow.Game.Objects.Players.Managers.SettingsManager',$true)
$manager=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($type)
foreach($id in @('ammunition_laser_lcb-10','ammunition_rocket_r-310')){
 $status=$type.GetMethod('GetBuyItemStatus').Invoke($manager,@($id))
 if($status.var_1474 -ne ('quickbuy_'+$id)){throw 'Quick-buy entry collides with ammunition ID'}
 if($status.iconLootId -ne $id){throw 'Quick-buy icon must use the original ammunition asset'}
 if(!$status.visible -or !$status.available){throw 'Quick-buy entry unavailable'}
 $bytes=$status.writeCommand();if($bytes.Length -lt 10){throw 'Missing standalone status packet'}
}
Write-Output 'Quick-buy regression passed: X1 and R-310 have independent buy IDs and valid ammunition icons.'
