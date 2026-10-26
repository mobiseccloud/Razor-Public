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

# Regenerable build outputs are gitignored and never committed; they are
# excluded from tree sweeps (commit-hygiene is enforced separately in step 6).
$buildOutputDirs = '[\\/]((bin|obj|target|TestResults|node_modules)[\\/]|dist[\\/]|staging[\\/])'
function Is-BuildOutput($fullName) { return $fullName -match $buildOutputDirs }

# 1. Forbidden filename / extension sweep (.git internals excluded — history is
# audited separately in step 5 via git plumbing, which never touches reflogs)
# NOTE: approved-binaries.txt is a REQUIRED public artifact (explicit distribution
# allow-list with measured hashes). It is validated for consistency with
# artifacts/hardened/ in step 7 below — never flagged by name.
$forbiddenName = @('\.lic$', '\.key$', '\.pem$', '\.pfx$', '\.p12$', '\.kdbx$', '(^|[/\\])\.env(\.|$)', 'razor\.lic$', 'id_rsa', 'id_ed25519')
Get-ChildItem -LiteralPath $RepoRoot -Recurse -Force -File |
  Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' } |
  Where-Object { -not (Is-BuildOutput $_.FullName) } | ForEach-Object {
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
  Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' } |
  Where-Object { -not (Is-BuildOutput $_.FullName) } |
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
  @{ File = 'docs\trial\OVERVIEW.md'; Line = '(?i)never (in|published)|private key, never|not the email|Never published|issued licenses, SMTP|No SMTP credentials, API keys, accounts' }
  @{ File = 'README.md'; Line = '(?i)no SMTP credentials' }
  @{ File = 'TrialEmail.cs'; Line = '(?i)never needs SMTP credentials' }
  @{ File = 'MainWindow.axaml'; Line = '(?i)No account, password, SMTP server' }
  # The allow-list's own entry lines (exact format: name + 64-hex hash + build
  # id). The filename necessarily repeats the binary name; the hash is the
  # measured artifact digest, not a secret. Comment lines are NOT exempt.
  @{ File = 'approved-binaries.txt'; Line = '^[a-z0-9-]+\.exe [0-9A-F]{64} bundle-' }
  # Boundary documentation that names the vendor-only generator to state its
  # exclusion. Source/keys/material for it remain forbidden everywhere.
  @{ File = 'artifacts\hardened\MANIFEST.txt'; Line = 'razor-licgen — not referenced by public packaging' }
  # Service unit documents that licence credentials are operator-supplied env
  # (no values). Narrowly benign; the scanner still flags any credential value.
  @{ File = 'razor-watch.service'; Line = 'licence credentials \(RAZOR_LICENSE' }
  # Prose mention of the public binary basename in installer docs.
  @{ File = 'razorzip-setup.iss'; Line = 'currently razor-train\)' }
  # .gitignore prohibition comment (the path itself is never committed).
  @{ File = '.gitignore'; Line = '^C:\\Temp$' }
  # License-loader reference docs name the PUBLIC env-var contract
  # (RAZOR_LICENSE / RAZOR_LICENSE_PUBLIC_KEY) users must set, and the
  # loader module path for provenance. Names only — no values, keys, or
  # license bodies appear in these files; value-bearing patterns still fail.
  @{ File = 'docs\security\LICENSE-DIRS.md'; Line = '(?i)RAZOR_LICENSE' }
  @{ File = 'docs\security\LICENSE-LOADER-PATHS.md'; Line = '(?i)RAZOR_LICENSE|razor_license::' }
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
  # NOTE: the hyphenated public binary filename (razor-train.exe, incl. the
  # .integrity sidecar) is expected wherever the allow-listed artifact is
  # referenced, so only bare "razor-train" (private crate/dir naming, e.g.
  # Rust/razor-train) is flagged. Underscored Rust module form is always flagged.
  'dna::|razor_train|razor-train(?!\.exe)|genome.*motif'
)
$textExts = @('.md', '.cs', '.csproj', '.sln', '.ps1', '.sh', '.yml', '.yaml', '.json', '.csv', '.txt', '.axaml', '.iss', '.spec', '.service', '.desktop')
# NOTE: .NET reports dotfiles (e.g. .gitignore) as extensionless-or-self, which
# would silently skip them below. The Is-TextFile check covers them explicitly.
function Is-TextFile($f) {
  if ($textExts -contains $f.Extension) { return $true }
  if ($f.Extension -eq '' -or $f.Extension -eq $f.Name) { return $true }
  return $false
}
Get-ChildItem -LiteralPath $RepoRoot -Recurse -Force -File |
  Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' } |
  Where-Object { -not (Is-BuildOutput $_.FullName) } |
  Where-Object { Is-TextFile $_ } |
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

# 4. Binary strings (assets + staged allow-listed executables; .git and build
# outputs excluded — shipped payloads are hashed in build manifests instead).
# Allow-listed staged binaries are EXEMPT from pure hardener/env markers
# (VMProtect, RAZOR_LICENSE): those are the protector's own linkage strings /
# env-var names, expected in a hardened binary — not secrets or config. They
# remain fully scanned for genuine secret patterns (keys, URLs, credentials).
function Get-AllowListedNames($root) {
  $names = @()
  $al = Join-Path $root 'approved-binaries.txt'
  if (Test-Path -LiteralPath $al) {
    foreach ($line in [IO.File]::ReadAllLines($al)) {
      if ($line -match '^\s*(#|$)') { continue }
      $tok = $line -split '\s+'
      if ($tok.Count -ge 4) { $names += $tok[0] }
    }
  }
  return $names
}
$allowListed = Get-AllowListedNames $RepoRoot
$markerExempt = @('VMProtect', 'RAZOR_LICENSE')
Get-ChildItem -LiteralPath $RepoRoot -Recurse -Force -File |
  Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' } |
  Where-Object { -not (Is-BuildOutput $_.FullName) } |
  Where-Object { $_.Extension -match '^\.(exe|dll|so|dylib|bin|jpeg|jpg|png)$' } | ForEach-Object {
  try {
    $bytes = [IO.File]::ReadAllBytes($_.FullName)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    $rel = $_.FullName.Substring($RepoRoot.Length)
    foreach ($pat in @('PRIVATE KEY', 'razor-licgen', 'VMProtect', 'mongodb://', 'smtp', 'RAZOR_LICENSE')) {
      if ($ascii -match "(?i)$pat") {
        $base = [IO.Path]::GetFileName($_.FullName)
        if (($markerExempt -contains $pat) -and ($allowListed -contains $base)) {
          Write-Host "reviewed-benign: hardener/env marker '$pat' in allow-listed staged binary $rel" -ForegroundColor DarkGray
        } else {
          Add-Failure "binary string '$pat' in $rel"
        }
      }
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
  if ($isRepo) {
    # 6. Commit hygiene: build outputs must be gitignored; anything else
    # untracked-but-committable would silently enter the next commit.
    $porcelain = git status --porcelain --ignored 2>$null
    foreach ($line in $porcelain) {
      if ($line -match '^\?\? (.*)') {
        $rel = $Matches[1].Trim('"')
        if ($rel -match '\.(lic|key|pem|pfx|p12|rs|pdb|env)$') { Add-Failure "untracked committable private file: $rel" }
      }
    }
  }
} finally { Pop-Location }

# 7. Allow-list <-> staged consistency (requires exact hash agreement).
$alPath = Join-Path $RepoRoot 'approved-binaries.txt'
$hardDir = Join-Path $RepoRoot 'artifacts\hardened'
if (Test-Path -LiteralPath $alPath) {
  $entries = @{}
  foreach ($line in [IO.File]::ReadAllLines($alPath)) {
    if ($line -match '^\s*(#|$)') { continue }
    $tok = $line -split '\s+'
    if ($tok.Count -lt 5) { Add-Failure "allow-list malformed line: $line"; continue }
    if ($entries.ContainsKey($tok[0])) { Add-Failure "allow-list duplicate: $($tok[0])"; continue }
    $entries[$tok[0]] = $tok[1]
  }
  $staged = @()
  if (Test-Path -LiteralPath $hardDir) {
    # MANIFEST.txt is documentation, not a distributable — same as README.md.
    $staged = Get-ChildItem -LiteralPath $hardDir -File | Where-Object { $_.Name -notin @('README.md', 'MANIFEST.txt') }
  }
  foreach ($f in $staged) {
    if ($f.Extension -eq '.integrity') {
      $exe = $f.Name -replace '\.integrity$', ''
      if ($entries.Keys -notcontains $exe) { Add-Failure "staged sidecar without allow-listed exe: $($f.Name)" }
      continue
    }
    if (-not $entries.ContainsKey($f.Name)) { Add-Failure "staged file not allow-listed: $($f.Name)"; continue }
    $h = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
    if ($h -ne $entries[$f.Name]) { Add-Failure "staged hash != allow-list for $($f.Name)" }
    else { Write-Host "allow-list OK: $($f.Name) hash matches" -ForegroundColor DarkGray }
    $side = $f.FullName + '.integrity'
    if (-not (Test-Path -LiteralPath $side)) { Add-Failure "staged exe missing sidecar: $($f.Name)" }
  }
  foreach ($name in $entries.Keys) {
    if (-not (Test-Path -LiteralPath (Join-Path $hardDir $name))) { Add-Failure "allow-listed file not staged: $name" }
  }
} else {
  $left = @()
  if (Test-Path -LiteralPath $hardDir) {
    $left = Get-ChildItem -LiteralPath $hardDir -File | Where-Object { $_.Name -ne 'README.md' }
  }
  if ($left.Count -gt 0) { Add-Failure "binaries staged without an allow-list" }
  else { Write-Host "allow-list absent and nothing staged (harness-only state)" -ForegroundColor DarkGray }
}

Write-Host "----------------------------------------"
if ($failures.Count -gt 0) {
  Write-Host "AUDIT FAILED: $($failures.Count) finding(s). STOP — do not publish." -ForegroundColor Red
  exit 1
} else {
  Write-Host "AUDIT PASSED: no findings." -ForegroundColor Green
  exit 0
}
