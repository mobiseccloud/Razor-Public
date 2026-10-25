<#
.SYNOPSIS
  Post-staging verification: CSV schema, manifests, integrity gate, harness build.
  Non-zero exit on failure. Tamper/integrity checks SKIP cleanly when no
  allow-list binaries are present (this release).
#>
param(
  [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
$failures = @()
function Fail($m) { $script:failures += $m; Write-Host "FAIL: $m" -ForegroundColor Red }
function Pass($m) { Write-Host "PASS: $m" -ForegroundColor Green }
function Skip($m) { Write-Host "SKIP: $m" -ForegroundColor Yellow }

$requiredHeader = 'suite,corpus,file,engine,tier,original_bytes,compressed_bytes,ratio_percent,encode_ms,decode_ms,encode_mb_s,decode_mb_s,round_trip,status,timestamp_utc,host,threads,binary_version,format_version'
$validStatus = @('PASS', 'RT-FAIL', 'ERROR', 'TIMEOUT', 'SKIPPED')

# 1. CSV schema + invariants
$csvs = Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'docs\benchmarks') -Filter '*.csv' -File -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -notlike 'manifest-*' }
if (-not $csvs -or $csvs.Count -eq 0) { Fail 'no benchmark CSVs in docs/benchmarks' }
else {
  foreach ($c in $csvs) {
    $lines = Get-Content -LiteralPath $c.FullName
    if ($lines.Count -lt 2) { Fail "$($c.Name): empty"; continue }
    if ($lines[0].Trim() -ne $requiredHeader) { Fail "$($c.Name): header mismatch. Got: $($lines[0])"; continue }
    $rows = Import-Csv -LiteralPath $c.FullName
    $rtFails = 0
    foreach ($r in $rows) {
      if ($validStatus -notcontains $r.status) { Fail "$($c.Name)/$($r.file): bad status '$($r.status)'" }
      $ob = [int64]$r.original_bytes; $cb = [int64]$r.compressed_bytes
      if ($ob -le 0 -or $cb -le 0) { Fail "$($c.Name)/$($r.file): non-positive sizes" }
      $expect = [math]::Round(100.0 * $cb / $ob, 2)
      if ([math]::Abs($expect - [double]$r.ratio_percent) -gt 0.02) { Fail "$($c.Name)/$($r.file): ratio mismatch ($($r.ratio_percent) vs $expect)" }
      if ($r.round_trip -eq 'MISMATCH') { $rtFails++ }
    }
    if ($rtFails -gt 0) { Fail "$($c.Name): $rtFails round-trip MISMATCH rows" }
    else { Pass "$($c.Name): $($rows.Count) rows, schema+ratio+round-trip OK" }
  }
}

# 2. Manifests present
foreach ($m in @('manifest-suite1.csv', 'manifest-suite2.csv')) {
  $p = Join-Path $RepoRoot "docs\benchmarks\$m"
  if (Test-Path -LiteralPath $p) { Pass "manifest $m present" } else { Fail "manifest $m missing" }
}

# 3. Commercial binary gate
$binDir = Join-Path $RepoRoot 'artifacts\hardened'
$binaries = Get-ChildItem -LiteralPath $binDir -File -ErrorAction SilentlyContinue |
  Where-Object { $_.Extension -notin @('.md') }
if (-not $binaries -or $binaries.Count -eq 0) {
  Skip 'no commercial binaries (no allow-list) — integrity/tamper checks N/A this release'
} else {
  foreach ($b in $binaries) {
    $sidecar = "$($b.FullName).integrity"
    if (-not (Test-Path -LiteralPath $sidecar)) { Fail "$($b.Name): missing .integrity sidecar" }
    else {
      $h = (Get-FileHash -LiteralPath $b.FullName -Algorithm SHA256).Hash
      $s = Get-Content -LiteralPath $sidecar -Raw
      if ($s -match $h) { Pass "$($b.Name): integrity sidecar matches" } else { Fail "$($b.Name): integrity mismatch" }
    }
  }
}

# 4. Harness builds
$sln = Join-Path $RepoRoot 'harness\RazorBenchmark\RazorBenchmark.sln'
if (Test-Path -LiteralPath $sln) {
  try {
    dotnet build $sln -c Release --nologo -v q 2>&1 | Select-Object -Last 5
    if ($?) { Pass 'harness builds (Release)' } else { Fail 'harness build failed' }
  } catch { Fail "harness build exception: $_" }
} else { Fail 'harness solution missing' }

# 5. Required docs present
foreach ($d in @('README.md','LICENSE','SECURITY.md','SUPPORT.md','CHANGELOG.md','BENCHMARKS.md','CORPUS.md','FORMAT.md','ARCHITECTURE.md')) {
  if (Test-Path -LiteralPath (Join-Path $RepoRoot $d)) { Pass "doc $d present" } else { Fail "doc $d missing" }
}

Write-Host '----------------------------------------'
if ($failures.Count -gt 0) { Write-Host "VERIFY FAILED: $($failures.Count) failure(s)." -ForegroundColor Red; exit 1 }
Write-Host 'VERIFY PASSED.' -ForegroundColor Green
exit 0
