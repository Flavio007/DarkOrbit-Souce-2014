param([string]$Compiler = 'D:\MS\VSC\MSBuild\Current\Bin\Roslyn\csc.exe')
$ErrorActionPreference = 'Stop'
$serverRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('do-network-regression-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$output = Join-Path $testRoot 'NetworkRegression.exe'
& $Compiler /nologo /r:System.Data.dll "/out:$output" (Join-Path $PSScriptRoot 'NetworkStabilityRegression.cs') (Join-Path $serverRoot 'Net\GameClient.cs') (Join-Path $serverRoot 'Net\ClientPacketBuffer.cs') (Join-Path $serverRoot 'Utils\Logger.cs')
if ($LASTEXITCODE -ne 0) { throw 'Network regression host compilation failed' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Network regression failed' }
Write-Output "Isolated test logs: $testRoot"
