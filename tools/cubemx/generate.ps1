param(
    [string]$CubeMxRoot = 'D:\STM32',
    [ValidatePattern('^[A-Z]$')][string]$DriveLetter = 'O'
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$driveName = $DriveLetter + ':'
$mappedRoot = $driveName + '\'
$javaPath = Join-Path $CubeMxRoot 'jre/bin/java.exe'
$cubePath = Join-Path $CubeMxRoot 'STM32CubeMX.exe'
if (!(Test-Path -LiteralPath $javaPath) -or !(Test-Path -LiteralPath $cubePath)) {
    throw 'Installed CubeMX and its bundled Java are required; no tools will be installed.'
}
if (Test-Path -LiteralPath $mappedRoot) {
    throw "Drive $driveName is already in use. Choose an unused -DriveLetter."
}
$buildDirectory = Join-Path $workspaceRoot 'build'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$commandPath = Join-Path $buildDirectory 'cubemx-generate.script'
$logPath = Join-Path $buildDirectory 'cubemx-generate.log'
$cubeProject = "$DriveLetter`:/firmware/Othello_Basic"
$commands = @(
    "config load $cubeProject/Othello_Basic.ioc",
    'project name Othello_Basic',
    'project toolchain MDK-ARM V5',
    "project path $cubeProject",
    'project couplefilesbyip 1',
    'SetCopyLibrary "copy only"',
    'project generate',
    "config save $cubeProject/Othello_Basic.ioc",
    'exit'
)
[IO.File]::WriteAllLines($commandPath, $commands, [Text.UTF8Encoding]::new($false))
$mappedByThisRun = $false
try {
    & subst.exe $driveName $workspaceRoot
    if ($LASTEXITCODE -ne 0) { throw 'Cannot create the temporary workspace drive mapping.' }
    $mappedByThisRun = $true
    Write-Output "CubeMX workspace alias: $driveName -> $workspaceRoot"
    $loggingConfig = "-Dlog4j.configuration=file:/$DriveLetter`:/tools/cubemx/log4j.properties"
    & $javaPath $loggingConfig -jar $cubePath -q "$mappedRoot/build/cubemx-generate.script" 2>&1 |
        Tee-Object -FilePath $logPath |
        Where-Object {
            $_ -match '^(OK|KO|exit|Bye|config|project|SetCopyLibrary)|SAXParseException|Exception|generat|Generate|success' -and
            $_ -notmatch 'Invalid condition id'
        } | ForEach-Object { Write-Output $_ }
    if ($LASTEXITCODE -ne 0) { throw "CubeMX failed; see $logPath" }
    $diagnostics = Get-Content -LiteralPath $logPath -Raw
    if ($diagnostics -match '(?m)^KO\s*$|SAXParseException|java\.[\w.]*Exception') {
        throw "CubeMX reported a generation error; see $logPath"
    }
    foreach ($relativeFile in @('Core/Src/main.c', 'Core/Src/tim.c', 'Core/Src/usart.c', 'Core/Src/gpio.c', 'Core/Src/dma.c', 'MDK-ARM/Othello_Basic.uvprojx')) {
        if (!(Test-Path -LiteralPath (Join-Path "$workspaceRoot/firmware/Othello_Basic" $relativeFile))) {
            throw "Missing generated file: $relativeFile"
        }
    }
    # CubeMX 6.3 can retain a mapped absolute source path after a failed generation.
    # Make the generated MDK project portable and reject any missing source file.
    $mdkDirectory = Join-Path $workspaceRoot 'firmware/Othello_Basic/MDK-ARM'
    $mdkProjectPath = Join-Path $mdkDirectory 'Othello_Basic.uvprojx'
    $mdkText = [IO.File]::ReadAllText($mdkProjectPath)
    [IO.File]::WriteAllText((Join-Path $buildDirectory 'cubemx-native.uvprojx'), $mdkText, [Text.UTF8Encoding]::new($false))
    $mdkText = $mdkText -replace '(?<=<FilePath>)\.\./[A-Z]:/firmware/Othello_Basic/', '../'
    $mdkText = $mdkText -replace 'Application/User/[A-Z]:/firmware/Othello_Basic/', 'Application/User/'
    $seenSources = @{}
    $mdkText = [regex]::Replace($mdkText, '(?s)<File>.*?</File>', [Text.RegularExpressions.MatchEvaluator]{
        param($fileMatch)
        $pathMatch = [regex]::Match($fileMatch.Value, '<FilePath>(.*?)</FilePath>')
        if (!$pathMatch.Success) { return $fileMatch.Value }
        $sourcePath = $pathMatch.Groups[1].Value.Replace('\', '/')
        if ($seenSources.ContainsKey($sourcePath.ToLowerInvariant())) {
            return ''
        }
        $seenSources[$sourcePath.ToLowerInvariant()] = $true
        return $fileMatch.Value
    })
    # Preserve CubeMX's native XML formatting for the installed uVision parser.
    [IO.File]::WriteAllText($mdkProjectPath, $mdkText, [Text.UTF8Encoding]::new($false))
    [xml]$mdkProject = $mdkText
    foreach ($sourcePathNode in @($mdkProject.SelectNodes('//FilePath'))) {
        $sourcePath = $sourcePathNode.InnerText
        if (!(Test-Path -LiteralPath (Join-Path $mdkDirectory $sourcePath))) {
            throw "Generated MDK source path does not exist: $sourcePath"
        }
    }
    $firmwarePackageRoot = Join-Path $env:USERPROFILE 'STM32Cube/Repository/STM32Cube_FW_F1_V1.8.6'
    foreach ($licenseRelativePath in @('License.md', 'Drivers/STM32F1xx_HAL_Driver/LICENSE.txt', 'Drivers/CMSIS/LICENSE.txt', 'Drivers/CMSIS/Device/ST/STM32F1xx/LICENSE.txt')) {
        $licenseSource = Join-Path $firmwarePackageRoot $licenseRelativePath
        if (!(Test-Path -LiteralPath $licenseSource)) {
            throw "Retain the package license from the actual firmware package: $licenseSource"
        }
        Copy-Item -LiteralPath $licenseSource -Destination (Join-Path "$workspaceRoot/firmware/Othello_Basic" $licenseRelativePath) -Force
    }
    Write-Output "CubeMX generated the complete project. Log: $logPath"
} finally {
    if ($mappedByThisRun) {
        & subst.exe $driveName /d
        if ($LASTEXITCODE -ne 0) { Write-Warning "Remove the temporary mapping with: subst $driveName /d" }
    }
}
