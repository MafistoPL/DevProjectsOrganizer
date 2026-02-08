param(
  [string]$InputDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs\excalidraw'),
  [string]$OutputDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs\images'),
  [int]$Scale = 2,
  [string]$CachePath = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($CachePath)) {
  $CachePath = Join-Path $OutputDir '.excalidraw-export-cache.json'
}

if (-not (Test-Path $InputDir)) {
  throw "Input folder not found: $InputDir"
}

if (-not (Test-Path $OutputDir)) {
  New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

$cache = @{}
if (Test-Path $CachePath) {
  try {
    $cacheObj = Get-Content -LiteralPath $CachePath -Raw | ConvertFrom-Json
    if ($cacheObj) {
      foreach ($prop in $cacheObj.PSObject.Properties) {
        $cache[$prop.Name] = $prop.Value
      }
    }
  } catch {
    Write-Warning "Cache file is invalid. Starting fresh: $CachePath"
    $cache = @{}
  }
}

$exported = 0
$skipped = 0
$existingPaths = @{}

Get-ChildItem -LiteralPath $InputDir -Filter *.excalidraw | ForEach-Object {
  $file = $_
  $existingPaths[$file.FullName] = $true
  $out = Join-Path $OutputDir ($file.BaseName + '.png')

  $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
  $cacheEntry = $cache[$file.FullName]

  $shouldExport = $true
  if ($cacheEntry -and $cacheEntry.hash -eq $hash -and (Test-Path $out)) {
    $shouldExport = $false
  }

  if ($shouldExport) {
    npx --yes excalidraw-brute-export-cli -i $file.FullName --format png -s $Scale -o $out
    $cache[$file.FullName] = @{
      hash = $hash
      updatedAt = (Get-Date).ToString('o')
    }
    $exported++
  } else {
    $skipped++
  }
}

# Remove cache entries for files that no longer exist
foreach ($key in @($cache.Keys)) {
  if (-not $existingPaths.ContainsKey($key)) {
    $cache.Remove($key)
  }
}

$tmp = "$CachePath.tmp"
$cache | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $tmp -Encoding UTF8
Move-Item -LiteralPath $tmp -Destination $CachePath -Force

Write-Host "Export complete. Exported: $exported, skipped: $skipped"
