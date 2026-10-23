param(
    [string]$Destination = 'G:/mcp/Fellowship'
)

$ErrorActionPreference = 'Stop'

$repository = Resolve-Path (Join-Path $PSScriptRoot '../..')
$staging = Join-Path ([IO.Path]::GetTempPath()) "fellowship-codex-mcp-$PID"

dotnet publish (Join-Path $repository 'src/Fellowship.Codex.Mcp') -c Release -o $staging
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force $Destination | Out-Null

Get-ChildItem $Destination -Recurse -File -Filter '*.replaced' | ForEach-Object {
    try { Remove-Item $_.FullName -Force -ErrorAction Stop } catch {}
}

$published = Get-ChildItem $staging -Recurse -File |
    ForEach-Object { [IO.Path]::GetRelativePath($staging, $_.FullName) }

foreach ($stale in Get-ChildItem $Destination -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($Destination, $stale.FullName)
    if ($stale.Name.EndsWith('.replaced') -or $published -contains $relative) { continue }

    try {
        Remove-Item $stale.FullName -Force -ErrorAction Stop
    }
    catch [IO.IOException] {
        Move-Item $stale.FullName "$($stale.FullName).$([guid]::NewGuid().ToString('N')).replaced" -Force
    }
}

Get-ChildItem $Destination -Recurse -Directory |
    Sort-Object { $_.FullName.Length } -Descending |
    Where-Object { -not (Get-ChildItem $_.FullName -Force) } |
    Remove-Item

foreach ($source in Get-ChildItem $staging -Recurse -File) {
    $target = Join-Path $Destination ([IO.Path]::GetRelativePath($staging, $source.FullName))
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null

    try {
        Copy-Item $source.FullName $target -Force -ErrorAction Stop
    }
    catch [IO.IOException] {
        Move-Item $target "$target.$([guid]::NewGuid().ToString('N')).replaced" -Force
        Copy-Item $source.FullName $target -Force
    }
}

Remove-Item $staging -Recurse -Force
