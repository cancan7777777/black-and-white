$ErrorActionPreference = 'Stop'
$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectRoot = Join-Path $workspaceRoot 'firmware/Othello_Basic'
$manifest = Join-Path $projectRoot 'application_sources.txt'
if (!(Test-Path -LiteralPath $manifest)) { return }
$projectFile = Join-Path $projectRoot 'MDK-ARM/Othello_Basic.uvprojx'
$text = [IO.File]::ReadAllText($projectFile)
$entries = @()
$nl = [Environment]::NewLine
foreach ($relative in Get-Content -LiteralPath $manifest) {
    $relative = $relative.Trim()
    if (!$relative -or $relative.StartsWith('#')) { continue }
    if ($relative -notmatch '^Core/Src/[a-zA-Z0-9_]+\.c$') { throw "Invalid application source: $relative" }
    if (!(Test-Path -LiteralPath (Join-Path $projectRoot $relative))) { throw "Missing source: $relative" }
    $path = '../' + $relative
    if ($text.Contains('<FilePath>' + $path + '</FilePath>')) { continue }
    $name = [IO.Path]::GetFileName($relative)
    $entries += (@('<File>', "  <FileName>$name</FileName>", '  <FileType>1</FileType>', "  <FilePath>$path</FilePath>", '</File>') -join $nl)
}
if ($entries.Count) {
    $group = (@('<Group>', '<GroupName>Application/Othello</GroupName>', '<Files>', ($entries -join $nl), '</Files>', '</Group>') -join $nl) + $nl
    $text = $text.Replace('</Groups>', $group + '</Groups>')
    [IO.File]::WriteAllText($projectFile, $text, [Text.UTF8Encoding]::new($false))
}
Write-Output 'Application source manifest synchronized.'
