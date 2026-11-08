param([string]$ServerAssembly='H:/Darkorbit-Cpp/build/server-events/DarkOrbit.exe')
$ErrorActionPreference='Stop'
$output=Join-Path $env:TEMP ('mineral-regression-'+[Guid]::NewGuid().ToString('N')+'.exe')
& "$env:WINDIR/Microsoft.NET/Framework/v4.0.30319/csc.exe" /nologo "/out:$output" (Join-Path $PSScriptRoot 'MineralVeinsRegression.cs')
if($LASTEXITCODE){throw 'Mineral test compile failed'}
& $output $ServerAssembly
if($LASTEXITCODE){throw 'Mineral test failed'}
