param([string]$VisualStudioRoot = 'D:\VISUAL STUDIO\vs 2022\community')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$environmentBatch = Join-Path $VisualStudioRoot 'VC/Auxiliary/Build/vcvars64.bat'
$environmentLines = & cmd.exe /d /s /c ('"' + $environmentBatch + '" >nul && set')
if ($LASTEXITCODE -ne 0) { throw 'VS environment setup failed' }
foreach ($line in $environmentLines) {
    $split = $line.IndexOf('=')
    if ($split -gt 0) { [Environment]::SetEnvironmentVariable($line.Substring(0,$split),$line.Substring($split+1),'Process') }
}
$build = Join-Path $root 'build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$source = Join-Path $root 'firmware/Othello_Basic/Core/Src'
$include = Join-Path $root 'firmware/Othello_Basic/Core/Inc'
& cl.exe /nologo /std:c11 /W3 "/I$include" "$source/app_basic.c" "$source/othello.c" "$source/ot_protocol.c" "$root/tests/test_app_basic.c" "/Fe:$build/test_app_basic.exe" "/Fo:$build/"
if ($LASTEXITCODE -ne 0) { throw 'Application tests did not compile' }
& "$build/test_app_basic.exe"
if ($LASTEXITCODE -ne 0) { throw 'Application tests failed' }
& cl.exe /nologo /std:c11 /W3 "/I$include" "/FI$root/tests/platform_hal_stub.h" "$source/app_platform.c" "$root/tests/test_app_platform.c" "/Fe:$build/test_app_platform.exe" "/Fo:$build/"
if ($LASTEXITCODE -ne 0) { throw 'Platform tests did not compile' }
& "$build/test_app_platform.exe"
if ($LASTEXITCODE -ne 0) { throw 'Platform tests failed' }
