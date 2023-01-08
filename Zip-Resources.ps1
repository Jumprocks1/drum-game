# Creates resources.zip which can then be distributed as part of a release

$ErrorActionPreference = "Stop"

$resources = Resolve-Path resources
Write-Host "Resources: $resources" -ForegroundColor Magenta

$outputPath = Join-Path (Resolve-Path "releases") "resources.zip"
Write-Host "Output: $outputPath" -ForegroundColor Cyan

[System.Collections.ArrayList]$targetFiles = @()

$globs = @(
    "filters/*",
    "fonts/*",
    "fonts/Noto/*",
    "repositories/*",
    "repositories/cache/*",
    "sh_Shaders/*",
    "soundfonts/licenses/*/*",
    "soundfonts/main.sf2",
    "README.txt",
)

foreach ($glob in $globs) {
    foreach ($f in Get-ChildItem "$resources/$glob" -File) {
        $targetFiles.Add($f.FullName) > $null
    }
}

$includeAudioArtists = "Dare I Dream","Resisting the Silence"
$includeAudio = ,"BRS.bjson"


$maps = "$resources/maps"

$files = Get-ChildItem $maps -File *.bjson

foreach ($f in $files) {
    $parsed = Get-Content $f | ConvertFrom-Json
    if (($parsed.mapper -eq $null) -or ($parsed.mapper.Contains("(WIP)"))) {continue;}
    if (($parsed.tags -ne $null) -and ($parsed.tags.contains("dtx-import"))) {continue;}
    if (($parsed.tags -ne $null) -and ($parsed.tags.contains("taiko"))) {continue;}

    $targetFiles.Add($f.FullName) > $null

    $artist = $parsed.artist;
    if ($includeAudio.Contains($f.Name) -or $includeAudioArtists.Contains($artist)) {
        $audioPath = Join-Path $maps $parsed.audio
        $targetFiles.Add($audioPath) > $null
    }

    # if ($targetFiles.Count -ge 5) {break;} # don't want to write too much to disk for no reason
}

function Format-FileSize() {
    Param ([int64]$size)
    [string]::Format("{0:0.00} MB", $size / 1MB)
}

Write-Host "Compressing..." -ForegroundColor DarkGray
if (Test-Path $outputPath) {
    Write-Host "$outputPath already exists, removing" -ForegroundColor Red
    Remove-Item $outputPath
}

$zip = [System.IO.Compression.ZipFile]::Open(($outputPath), [System.IO.Compression.ZipArchiveMode]::Create)

$relativeTo = "$resources/.." | Convert-Path

# write entries with relative paths as names
foreach ($f in $targetFiles) {
    # on Windows systems, this will make paths with \ in it. We don't want \ in our zip files.
    $relative = [System.IO.Path]::GetRelativePath($relativeTo, $f) -replace "\\","/"
    $entry = $zip.CreateEntry($relative)
    $writer = New-Object -TypeName System.IO.BinaryWriter $entry.Open()
    $writer.Write([System.IO.File]::ReadAllBytes($f))
    $writer.Close()
}
$zip.Dispose()

$size = Format-FileSize (Get-ChildItem $outputPath).Length
Write-Host "Compression complete, created $outputPath $size" -ForegroundColor Green