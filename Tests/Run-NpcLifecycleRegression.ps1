param([string]$ServerAssembly = (Join-Path $PSScriptRoot '..\bin\Release\DarkOrbit.exe'))
$ErrorActionPreference = 'Stop'
$ServerAssembly = (Resolve-Path -LiteralPath $ServerAssembly).Path
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('darkorbit-npc-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$testExecutable = Join-Path $testDirectory 'NpcLifecycleRegression.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo "/out:$testExecutable" (Join-Path $PSScriptRoot 'NpcLifecycleRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression harness compilation failed.' }
& $testExecutable $ServerAssembly
if ($LASTEXITCODE -ne 0) { throw 'NPC lifecycle regression failed.' }
