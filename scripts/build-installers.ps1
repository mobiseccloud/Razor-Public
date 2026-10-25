<#
.SYNOPSIS
  Hardened RazorZip installer build pipeline (Windows).
  Enforces the approved-binary allow-list gate; fails closed; never publishes.
#>
param(
  [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
  [string]$AllowList = $env:RAZOR_ALLOWLIST_PATH
)
$ErrorActionPreference = 'Stop'
$results = @()
$commercialOk = $false
function Rec($target, $status, $detail) {
  $script:results += [pscustomobject]@{ target = $target; status = $status; detail = $detail }
  Write-Host "[$status] $target :: $detail"
}
function Fail($t, $d) { Rec $t 'FAIL' $d }
function Skip($t, $d) { Rec $t 'SKIPPED' $d }
function Pass($t, $d) { Rec $t 'PASS' $d }
function Write-Manifest {
  $manifest = [pscustomobject]@{
    product = 'RazorZip'; version = '0.1.0'; platform = 'windows';
    commit = (git rev-parse HEAD 2>$null); commercial = $script:commercialOk;
    generated_utc = (Get-Date).ToUniversalTime().ToString('o'); results = $script:results
  }
  $manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $script:dist 'build-manifest-windows.json') -Encoding UTF8
  Write-Host "Manifest: dist/build-manifest-windows.json"
}

Set-Location $RepoRoot
$dist = Join-Path $RepoRoot 'dist'
$staging = Join-Path $RepoRoot 'staging'
New-Item -ItemType Directory -Path $dist, $staging -Force | Out-Null

# 1. Repo + toolchain audit (hard gate).
try {
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RepoRoot 'scripts\audit-public-release.ps1')
  if ($LASTEXITCODE -ne 0) { Fail 'audit' 'source-leak audit failed; STOP'; Write-Manifest; exit 1 }
  Pass 'audit' 'source-leak/secret/history audit passed'
} catch { Fail 'audit' $_.Exception.Message; Write-Manifest; exit 1 }

# 2. Allow-list gate. Default is the in-tree distribution boundary
# (approved-binaries.txt, format: filename sha256 version target purpose...);
# an explicit -AllowList path or RAZOR_ALLOWLIST_PATH overrides it (same format).
# Entries name exact filenames; hashes are cross-checked before staging.
$commercialFiles = @()
$allowHash = @{}
$allowPath = $AllowList
if ([string]::IsNullOrWhiteSpace($allowPath) -or -not (Test-Path -LiteralPath $allowPath)) {
  $inTree = Join-Path $RepoRoot 'approved-binaries.txt'
  if (Test-Path -LiteralPath $inTree) { $allowPath = $inTree }
}
if ([string]::IsNullOrWhiteSpace($allowPath) -or -not (Test-Path -LiteralPath $allowPath)) {
  Skip 'allow-list' 'no allow-list available; commercial targets disabled (fail-closed).'
} else {
  foreach ($line in (Get-Content -LiteralPath $allowPath)) {
    if ($line -match '^\s*(#|$)') { continue }
    $tok = $line -split '\s+'
    if ($tok.Count -lt 4) { Fail 'allow-list' "malformed allow-list line: $line"; Write-Manifest; exit 1 }
    $commercialFiles += $tok[0]
    $allowHash[$tok[0]] = $tok[1]
  }
  if ($commercialFiles.Count -eq 0) { Fail 'allow-list' 'allow-list empty/ambiguous; STOP'; Write-Manifest; exit 1 }
  Pass 'allow-list' "$($commercialFiles.Count) explicitly authorized artifact(s) from $(Split-Path -Leaf $allowPath)"
}

# 3-4. Stage harness (open, buildable) + verify commercial artifacts if authorized.
# Staging dirs are wiped first: dotnet publish merges into the output dir, so a
# reused staging tree would ship stale binaries from other RIDs (observed:
# win-x64 apphost + coreclr.dll leaking into a linux-x64 payload and breaking
# the runtime load). Never package a dirty staging tree.
try {
  $harnessStage = Join-Path $staging 'harness'
  if (Test-Path -LiteralPath $harnessStage) { Remove-Item -Recurse -Force $harnessStage }
  dotnet publish (Join-Path $RepoRoot 'harness\RazorBenchmark\RazorBenchmark\RazorBenchmark.csproj') `
    -c Release -r win-x64 --self-contained -o $harnessStage --nologo 2>&1 | Select-Object -Last 3
  if ($LASTEXITCODE -ne 0) { Fail 'stage-harness' 'dotnet publish failed'; Write-Manifest; exit 1 }
  # No debug symbols or foreign-RID apphosts in shipped payloads.
  Get-ChildItem -LiteralPath $harnessStage -Recurse -File -Include '*.pdb' | Remove-Item -Force
  if (Test-Path -LiteralPath (Join-Path $harnessStage 'RazorBenchmark.exe')) { Write-Host "win-x64 apphost present (expected)" -ForegroundColor DarkGray }
  Copy-Item (Join-Path $RepoRoot 'docs\trial\trial-request-template.txt') $harnessStage -Force
  Pass 'stage-harness' 'harness published (win-x64 self-contained, staging wiped first) + template staged'
} catch { Fail 'stage-harness' $_.Exception.Message; Write-Manifest; exit 1 }

$commercialOk = $false
if ($commercialFiles.Count -gt 0) {
  $ok = $true
  foreach ($f in $commercialFiles) {
    $src = Join-Path (Join-Path $RepoRoot 'artifacts\hardened') $f
    $side = "$src.integrity"
    if (-not (Test-Path -LiteralPath $src)) { Fail 'stage-commercial' "authorized artifact missing: $f"; $ok = $false; continue }
    if (-not (Test-Path -LiteralPath $side)) { Fail 'stage-commercial' "missing .integrity sidecar: $f"; $ok = $false; continue }
    $h = (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash
    if ($h -ne $allowHash[$f]) { Fail 'stage-commercial' "hash != allow-list for $f"; $ok = $false; continue }
    if (-not ((Get-Content -LiteralPath $side -Raw) -match $h)) { Fail 'stage-commercial' "integrity mismatch: $f"; $ok = $false; continue }
  }
  if (-not $ok) { Write-Manifest; exit 1 }
  New-Item -ItemType Directory -Path (Join-Path $staging 'commercial') -Force | Out-Null
  foreach ($f in $commercialFiles) {
    Copy-Item (Join-Path (Join-Path $RepoRoot 'artifacts\hardened') $f) (Join-Path $staging 'commercial') -Force
    Copy-Item ((Join-Path (Join-Path $RepoRoot 'artifacts\hardened') $f) + '.integrity') (Join-Path $staging 'commercial') -Force
  }
  $commercialOk = $true
  Pass 'stage-commercial' 'all authorized artifacts + sidecars verified and staged'
} else {
  Skip 'stage-commercial' 'no allow-list: commercial staging disabled (fail-closed)'
}

# 5. Inno Setup build (Windows).
$iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
if (-not $iscc) { Skip 'inno-setup' 'iscc.exe not on PATH (install Inno Setup 6). Scaffolding present: packaging/windows/razorzip-setup.iss' }
else {
  $issArgs = @((Join-Path $RepoRoot 'packaging\windows\razorzip-setup.iss'), ('/DHarnessDir=' + (Join-Path $staging 'harness')))
  if ($commercialOk) { $issArgs += '/DCOMMERCIAL' }
  & $iscc.Path @issArgs 2>&1 | Select-Object -Last 5
  if ($LASTEXITCODE -ne 0) { Fail 'inno-setup' 'iscc failed' } else { Pass 'inno-setup' 'setup exe built into dist/' }
}

# 6. WiX MSI: REMOVED as a deliverable (Windows delivery is Inno Setup only).
# The former RazorZip.wxs was deleted; its payload fragments were never
# generated and it was never compilable. Recorded as retired, not skipped.
Rec 'wix-msi' 'RETIRED' 'WiX/MSI removed as a deliverable (Inno Setup only)'

# 7. Harness selftest (evidence).
try {
  & (Join-Path $staging 'harness\RazorBenchmark.exe') --selftest 2>&1 | Select-Object -Last 3
  if ($LASTEXITCODE -ne 0) { Fail 'harness-selftest' 'published harness --selftest failed' } else { Pass 'harness-selftest' '--selftest OK on staged payload' }
} catch { Fail 'harness-selftest' $_.Exception.Message }

# 8-9. Collect dist/ + manifest.
Get-ChildItem (Join-Path $RepoRoot 'dist') -File -ErrorAction SilentlyContinue | ForEach-Object {
  $script:results += [pscustomobject]@{ target = 'artifact'; status = 'INFO';
    detail = "$($_.Name) sha256=$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)" }
}
Write-Manifest
if ($results | Where-Object { $_.status -eq 'FAIL' }) { exit 1 }
exit 0
