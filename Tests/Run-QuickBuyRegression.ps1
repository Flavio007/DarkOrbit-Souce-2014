param([string]$ServerAssembly='H:/Darkorbit-Cpp/build/server-events/DarkOrbit.exe')
$ErrorActionPreference='Stop'
$output=Join-Path $env:TEMP ('quickbuy-regression-'+[Guid]::NewGuid().ToString('N')+'.exe')
& "$env:WINDIR/Microsoft.NET/Framework/v4.0.30319/csc.exe" /nologo "/out:$output" (Join-Path $PSScriptRoot 'QuickBuyRegression.cs')
if($LASTEXITCODE){throw 'Quick-buy test compile failed'}
& $output $ServerAssembly
if($LASTEXITCODE){throw 'Quick-buy test failed'}
