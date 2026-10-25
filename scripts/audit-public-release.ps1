<#
.SYNOPSIS
  Red-team / source-leak audit for the PUBLIC release staging tree.
  Scans filenames, file contents, binary strings, git staged content and git
  history. Any finding => non-zero exit (STOP, do not publish).
#>
param(
  [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
$failures = @()

function Add-Failure($msg) { $script:failures += $msg; Write-Host "FAIL: $msg" -ForegroundColor Red }

Write-Host "=== audit-public-release (powershell) ==="
Write-Host "Root: $RepoRoot"

# 1. Forbidden filename / extension sweep
$forbiddenName = @('\.lic$', '\.key$', '\.pem$', '\.pfx$', '\.p12$', '\.kdbx$', '(^|[/\\])\.env(\.|$)', 'approved-binaries\.txt$', 'razor\.lic$', 'id_rsa', 'id_ed25519')
Get-ChildItem -LiteralPath $RepoRoot -Recurse -Force -File | ForEach-Object {
  $rel = $_.FullName.Substring($RepoRoot.Length)
  foreach ($pat in $forbiddenName) {
    if ($rel -match $pat) { Add-Failure "forbidden filename: $rel (pattern $pat)" }
  }
  if ($_.Extension -eq '.rs') { Add-Failure "proprietary Rust source present: $rel" }
  if ($_.Extension -eq '.pdb') { Add-Failure "PDB artifact present: $rel" }
}

# 2. Source allow-list: only harness/*.cs|csproj|sln, scripts/*, .github/*, docs/config/markdown/csv allowed as code
$allowedCode = @('^\\harness\\.*\.(cs|csproj|sln|axaml|axaml\.cs|resx|json)$', '^\\scripts\\.*$', '^\\.github\\.*$', '^\\.*\.md$', '^\\.*\.csv$', '^\\LICENSE$', '^\\.gitignore$')
Get-ChildItem -LiteralPath $RepoRoot -Recurse -Force -File |
  Where-Object { $_.Extension -match '^\.(cs|csproj|sln|rs|ps1|sh|yml|yaml|toml|go|java|py)$' } | ForEach-Object {
  $rel = $_.FullName.Substring($RepoRoot.Length)
  $ok = $false
  foreach ($pat in $allowedCode) { if ($rel -match $pat) { $ok = $true; break } }
  if (-not $ok) { Add-Failure "unexpected source file outside allow-list: $rel" }
}

# Reviewed benign exceptions: the matched line documents absence/prohibition,
# never a value. Each entry: file suffix + regex of the reviewed-benign line.
$benignExceptions = @(
  @{ File = 'LICENSE'; Line = '(?i)licensing service \(razor-licgen' },
  @{ File = 'README.md'; Line = '(?i)razor-licgen., signing keys' },
  @{ File = 'ARCHITECTURE.md'; Line = '(?i)never in this repo|are never|signing keys,' },
  @{ File = 'SECURITY.md'; Line = '(?i)No signing keys,|never in this repo|are never' },
  @{ File = 'docs\trial\OVERVIEW.md'; Line = '(?i)never (in|published)|private key, never|not the email|Never published|issued licenses, SMTP' }
)

function Is-Benign($relPath, $line) {
  foreach ($e in $benignExceptions) {
    if ($relPath -like ('*' + $e.File) -and $line -match $e.Line) { return $true }
  }
  return $false
}
$patterns = @(
  'BEGIN (RSA |OPENSSH |EC |DSA )?PRIVATE KEY',
  'PRIVATE KEY',
  'ssh-rsa AAAA',
  'AKIA[0-9A-Z]{16}',
  'xox[bpas]-',
  '-----BEGIN PGP',
  'razor-licgen',
  'VMProtect',
  'Reactor',
  'license-signing',
  'SIGNING_KEY',
  'smtp|SMTP',
  'api[_-]?key',
  'jwt[_-]?secret',
  'connection string',
  'mongodb(\+srv)?://',
  'postgres(ql)?://',
  'C:\\Temp',
  'C:/Temp',
  'D:\\TestMicro',
  'mobisec\.cloud@gmail\.com',
  'RAZOR_LICENSE',
  'RazorCompressor|Razor\.Compression|razor-compress|razor_bench|Binary DNA|StructuralLens|archival\.rs',
  'dna::|razor_train|razor-train|genome.*motif'
)
$textExts = @('.md', '.cs', '.csproj', '.sln', '.ps1', '.sh', '.yml', '.yaml', '.json', '.csv', '.txt', '.axaml')
Get-ChildItem -LiteralPath $RepoRoot -Recurse -Force -File |
  Where-Object { $textExts -contains $_.Extension -or $_.Extension -eq '' } |
  Where-Object { $_.FullName -notmatch 'scripts[\\/]audit-public-release\.(ps1|sh)$' } | ForEach-Object {
  try {
    $lines = [IO.File]::ReadAllLines($_.FullName)
    $rel = $_.FullName.Substring($RepoRoot.Length)
    foreach ($pat in $patterns) {
      foreach ($line in $lines) {
        if ($line -match "(?i)$pat") {
          if (Is-Benign $rel $line) {
            Write-Host "reviewed-benign: '$pat' in $rel :: $line" -ForegroundColor DarkGray
          } else {
            Add-Failure "content pattern '$pat' in $rel :: $line"
          }
          break
        }
      }
    }
  } catch { }
}

# 4. Binary strings (assets + any exe/dll, if ever present)
Get-ChildItem -LiteralPath $RepoRoot -Recurse -Force -File |
  Where-Object { $_.Extension -match '^\.(exe|dll|so|dylib|bin|jpeg|jpg|png)$' } | ForEach-Object {
  try {
    $bytes = [IO.File]::ReadAllBytes($_.FullName)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    $rel = $_.FullName.Substring($RepoRoot.Length)
    foreach ($pat in @('PRIVATE KEY', 'razor-licgen', 'VMProtect', 'mongodb://', 'smtp', 'RAZOR_LICENSE')) {
      if ($ascii -match "(?i)$pat") { Add-Failure "binary string '$pat' in $rel" }
    }
  } catch { }
}

# 5. Git staged content + history (only if this tree is a git repo)
Push-Location $RepoRoot
try {
  $isRepo = $false
  try { git rev-parse --git-dir 2>$null | Out-Null; $isRepo = ($?) } catch { $isRepo = $false }
  if ($isRepo) {
    $staged = git diff --cached --name-only 2>$null
    foreach ($f in $staged) {
      if ($f -match '\.(lic|key|pem|pfx|p12|rs|pdb)$') { Add-Failure "staged forbidden file: $f" }
    }
    $hist = git log --oneline -20 2>$null
    if ($hist -and ($hist | Select-String -Pattern 'licgen|private key|secret|credential' -Quiet)) {
      Add-Failure "suspicious git history content"
    }
    $priv = git log --all --diff-filter=A --name-only --pretty=format: 2>$null | Select-Object -Unique
    foreach ($f in $priv) {
      if ($f -match '\.(rs|lic|key|pem|pfx|p12)$') { Add-Failure "private file in git history: $f" }
    }
  } else {
    Write-Host "Not a git repo yet (expected pre-init) — history check deferred to post-init."
  }
} finally { Pop-Location }

Write-Host "----------------------------------------"
if ($failures.Count -gt 0) {
  Write-Host "AUDIT FAILED: $($failures.Count) finding(s). STOP — do not publish." -ForegroundColor Red
  exit 1
} else {
  Write-Host "AUDIT PASSED: no findings." -ForegroundColor Green
  exit 0
}
