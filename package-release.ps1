$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $projectRoot "build-release.bat"
$releaseDll = Join-Path $projectRoot "bin\Release\KK_BodyMaskLayers.dll"
$releasePdb = Join-Path $projectRoot "bin\Release\KK_BodyMaskLayers.pdb"
$distRoot = Join-Path $projectRoot "dist"
$archive = Join-Path $distRoot "KK_BodyMaskLayers_v0.1.2.zip"
$checksum = "$archive.sha256"

& $buildScript
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $releaseDll -PathType Leaf)) {
    throw "Release DLL was not produced."
}

if (Test-Path -LiteralPath $releasePdb) {
    throw "Release unexpectedly produced a PDB."
}

$assemblyText = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($releaseDll))
if ($assemblyText -match '(?i)[A-Z]:\\Users\\|\.pdb') {
    throw "Release DLL contains a local user path or PDB reference."
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

if (Test-Path -LiteralPath $archive) {
    Remove-Item -LiteralPath $archive -Force
}

Add-Type -AssemblyName System.IO.Compression
$archiveStream = [System.IO.File]::Open($archive, [System.IO.FileMode]::CreateNew)
$zip = New-Object System.IO.Compression.ZipArchive(
    $archiveStream,
    [System.IO.Compression.ZipArchiveMode]::Create,
    $false)

try {
    $entry = $zip.CreateEntry(
        "BepInEx/plugins/KK_BodyMaskLayers/KK_BodyMaskLayers.dll",
        [System.IO.Compression.CompressionLevel]::Optimal)
    $entry.LastWriteTime = [System.DateTimeOffset]::Parse("2000-01-01T00:00:00Z")
    $entryStream = $entry.Open()
    $dllStream = [System.IO.File]::OpenRead($releaseDll)
    try {
        $dllStream.CopyTo($entryStream)
    }
    finally {
        $dllStream.Dispose()
        $entryStream.Dispose()
    }
}
finally {
    $zip.Dispose()
    $archiveStream.Dispose()
}

$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
[System.IO.File]::WriteAllText($checksum, "$hash  KK_BodyMaskLayers_v0.1.2.zip`r`n")

Write-Output "Archive: $archive"
Write-Output "SHA256: $hash"
