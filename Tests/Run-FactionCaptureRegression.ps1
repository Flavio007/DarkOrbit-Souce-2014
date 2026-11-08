param([string]$ServerAssembly='H:/Darkorbit-Cpp/build/server-events/DarkOrbit.exe',[string]$FixturePath='H:/Darkorbit-Cpp/build/capture-fixture')
$ErrorActionPreference='Stop'
$output=Join-Path $env:TEMP ('capture-regression-'+[Guid]::NewGuid().ToString('N')+'.exe')
& "$env:WINDIR/Microsoft.NET/Framework/v4.0.30319/csc.exe" /nologo "/out:$output" (Join-Path $PSScriptRoot 'FactionCaptureRegression.cs')
if($LASTEXITCODE){throw 'Capture test compile failed'}
& $output $ServerAssembly $FixturePath
if($LASTEXITCODE){throw 'Capture test failed'}
