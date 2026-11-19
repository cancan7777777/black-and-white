param([string]$VisualStudioRoot = 'D:\VISUAL STUDIO\vs 2022\community')
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project=Join-Path $root 'labview/Othello_Basic'
$bin=Join-Path $project 'bin'
$build=Join-Path $root 'build/labview'
New-Item -ItemType Directory -Path $bin,$build -Force | Out-Null
$environmentBatch=Join-Path $VisualStudioRoot 'VC/Auxiliary/Build/vcvars32.bat'
$compilerEnvironment=& cmd.exe /d /s /c ('"'+$environmentBatch+'" >nul && set')
if($LASTEXITCODE -ne 0){throw 'Existing MSVC x86 environment unavailable'}
foreach($line in $compilerEnvironment){$separator=$line.IndexOf('=');if($separator -gt 0){[Environment]::SetEnvironmentVariable($line.Substring(0,$separator),$line.Substring($separator+1),'Process')}}
$inputCore=Join-Path $root '基础功能部分1/othello_basic/Core'
& cl.exe /nologo /LD /MT /O2 /W3 "/I$inputCore/Inc" "$project/src/NativeSimulator.c" "$inputCore/Src/othello.c" "$inputCore/Src/ot_protocol.c" "/Fo:$build\" "/Fe:$bin/OthelloSimulator.dll" /link "/DEF:$project/src/NativeSimulator.def" "/IMPLIB:$build/OthelloSimulator.lib"
if($LASTEXITCODE -ne 0){throw 'Native offline simulator build failed'}
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
$hostSources=@('Protocol.cs','Transport.cs','HostSession.cs','OthelloPanel.cs') | ForEach-Object { (Resolve-Path -LiteralPath (Join-Path $project ('src\'+$_))).Path }
& $csc /nologo /target:library /platform:x86 ("/out:"+(Join-Path $bin 'Othello.Basic.dll')) /r:System.Windows.Forms.dll /r:System.Drawing.dll @hostSources
if($LASTEXITCODE -ne 0){throw 'LabVIEW embedded control build failed'}
& $csc /nologo /platform:x86 /r:System.Windows.Forms.dll ("/out:"+(Join-Path $bin 'BasicTests.exe')) ("/r:"+(Join-Path $bin 'Othello.Basic.dll')) (Join-Path $project 'tests\BasicTests.cs')
if($LASTEXITCODE -ne 0){throw 'Host tests build failed'}
& "$bin/BasicTests.exe"
if($LASTEXITCODE -ne 0){throw 'Host tests failed'}
& $csc /nologo /platform:x86 /r:System.Web.Extensions.dll ("/out:"+(Join-Path $build 'LabViewAutomation.exe')) (Join-Path $root 'tools\LabViewAutomation.cs')
if($LASTEXITCODE -ne 0){throw 'LabVIEW automation helper build failed'}
