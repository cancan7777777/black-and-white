param([string]$UV4 = 'C:\Keil_v5\UV4\UV4.exe')

$ErrorActionPreference = 'Stop'
$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $workspaceRoot 'firmware/Othello_Basic/MDK-ARM/Othello_Basic.uvprojx'
if (!(Test-Path -LiteralPath $UV4)) { throw 'An existing Keil uVision installation is required.' }
if (!(Test-Path -LiteralPath $projectPath)) { throw 'Generate the CubeMX project first.' }
$buildDirectory = Join-Path $workspaceRoot 'build'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$logPath = Join-Path $buildDirectory 'keil-build.log'
$arguments = @('-b', ('"' + $projectPath + '"'), '-j0', '-o', ('"' + $logPath + '"'))
$startedAt = [DateTime]::UtcNow
$compilerProcess = Start-Process -FilePath $UV4 -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
Write-Output "KEIL_EXIT=$($compilerProcess.ExitCode)"
if (!(Test-Path -LiteralPath $logPath)) { throw 'Keil did not produce the expected build log.' }
if ((Get-Item -LiteralPath $logPath).LastWriteTimeUtc -lt $startedAt) {
    throw "Keil did not update its log (exit $($compilerProcess.ExitCode)); do not use the previous build result."
}
Get-Content -LiteralPath $logPath | Select-Object -Last 12
if ($compilerProcess.ExitCode -gt 1) { throw "Keil build failed. See $logPath" }
if ((Get-Content -LiteralPath $logPath -Raw) -notmatch '0 Error\(s\)') {
    throw "The Keil log does not confirm a successful build. See $logPath"
}
