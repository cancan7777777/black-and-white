param([string]$VisualStudioRoot = 'D:\VISUAL STUDIO\vs 2022\community')

$ErrorActionPreference = 'Stop'
$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$environmentBatch = Join-Path $VisualStudioRoot 'VC/Auxiliary/Build/vcvars64.bat'
if (!(Test-Path -LiteralPath $environmentBatch)) { throw 'An existing Visual Studio C compiler is required.' }
$compilerEnvironment = & cmd.exe /d /s /c ('"' + $environmentBatch + '" >nul && set')
if ($LASTEXITCODE -ne 0) { throw 'Visual Studio environment setup failed.' }
foreach ($line in $compilerEnvironment) {
    $separator = $line.IndexOf('=')
    if ($separator -gt 0) {
        [Environment]::SetEnvironmentVariable($line.Substring(0, $separator), $line.Substring($separator + 1), 'Process')
    }
}
$buildDirectory = Join-Path $workspaceRoot 'build'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$sourceDirectory = Join-Path $workspaceRoot '基础功能部分1/othello_basic'
$testExecutable = Join-Path $buildDirectory 'othello_core_tests.exe'
& cl.exe /nologo /std:c11 /W3 "/I$sourceDirectory/Core/Inc" `
    "$sourceDirectory/Core/Src/othello.c" "$sourceDirectory/Core/Src/ot_protocol.c" "$sourceDirectory/tests/test.c" `
    "/Fe:$testExecutable" "/Fo:$buildDirectory\"
if ($LASTEXITCODE -ne 0) { throw 'The supplied basic-core tests did not compile.' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'The supplied basic-core tests failed.' }
