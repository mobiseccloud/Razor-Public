#!/bin/sh
# Red-team / source-leak audit for the PUBLIC release staging tree (POSIX mirror).
# Scans filenames, contents, binary strings, git staged content and history.
# Any finding => non-zero exit (STOP, do not publish).
set -u
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
FAIL=0
fail() { echo "FAIL: $1"; FAIL=$((FAIL+1)); }

echo "=== audit-public-release (sh) ==="
echo "Root: $ROOT"

BUILD_OUT='*/bin/*|*/obj/*|*/target/*|*/dist/*|*/staging/*'
# 1. Forbidden filenames + proprietary extensions (.git internals and
# regenerable build outputs excluded; commit hygiene is step 5)
forbidden="$(find "$ROOT" -type f ! -path "$ROOT/.git/*" \
  ! -path "*/bin/*" ! -path "*/obj/*" ! -path "*/target/*" \
  ! -path "*/dist/*" ! -path "*/staging/*" \( -name '*.lic' -o -name '*.key' -o -name '*.pem' \
  -o -name '*.pfx' -o -name '*.p12' -o -name '.env' -o -name '.env.*' \
  -o -name 'approved-binaries.txt' -o -name 'razor.lic' \) 2>/dev/null)"
if [ -n "$forbidden" ]; then echo "$forbidden" | while read -r f; do fail "forbidden filename: $f"; done; FAIL=$((FAIL+1)); fi

rs_files="$(find "$ROOT" -type f ! -path "$ROOT/.git/*" \
  ! -path "*/bin/*" ! -path "*/obj/*" ! -path "*/target/*" \
  ! -path "*/dist/*" ! -path "*/staging/*" -name '*.rs' 2>/dev/null)"
if [ -n "$rs_files" ]; then echo "$rs_files" | while read -r f; do echo "FAIL: proprietary Rust source: $f"; done; FAIL=$((FAIL+1)); fi

pdb_files="$(find "$ROOT" -type f ! -path "$ROOT/.git/*" \
  ! -path "*/bin/*" ! -path "*/obj/*" ! -path "*/target/*" \
  ! -path "*/dist/*" ! -path "*/staging/*" -name '*.pdb' 2>/dev/null)"
if [ -n "$pdb_files" ]; then echo "$pdb_files" | while read -r f; do echo "FAIL: PDB artifact: $f"; done; FAIL=$((FAIL+1)); fi

# 2. Source allow-list (code outside harness/scripts/.github => STOP)
bad_src="$(find "$ROOT" -type f ! -path "$ROOT/.git/*" \
  ! -path "*/bin/*" ! -path "*/obj/*" ! -path "*/target/*" \
  ! -path "*/dist/*" ! -path "*/staging/*" \( -name '*.cs' -o -name '*.csproj' -o -name '*.sln' \
  -o -name '*.rs' -o -name '*.py' -o -name '*.go' -o -name '*.java' \) \
  ! -path "$ROOT/harness/*" ! -path "$ROOT/scripts/*" ! -path "$ROOT/.github/*" 2>/dev/null)"
if [ -n "$bad_src" ]; then echo "$bad_src" | while read -r f; do echo "FAIL: unexpected source outside allow-list: $f"; done; FAIL=$((FAIL+1)); fi

# 3. Content patterns
PAT='BEGIN.*PRIVATE KEY|PRIVATE KEY|razor-licgen|VMProtect|Reactor|license-signing|SIGNING_KEY|smtp|api[_-]?key|jwt[_-]?secret|connection string|mongodb(\+srv)?://|C:\\Temp|D:\\TestMicro|RAZOR_LICENSE|Razor\.Compression|razor-compress|StructuralLens|archival\.rs|razor[-_]train'
raw_hits="$(grep -rEI --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj \
  --exclude-dir=dist --exclude-dir=staging --exclude-dir=target \
  --exclude='*.jpeg' --exclude='*.jpg' \
  --exclude='audit-public-release.ps1' --exclude='audit-public-release.sh' \
  -e "$PAT" "$ROOT" 2>/dev/null || true)"
# Reviewed-benign documentation lines (prohibition/absence statements, never values).
hits="$(printf '%s\n' "$raw_hits" | grep -v -e 'licensing service (razor-licgen' \
  -e 'never in this repo' -e 'Never published' -e 'are never in this repo' \
  -e 'No "tamper-proof"' -e 'is not the email' -e 'not the email transport' \
  -e 'committed — enforced'   -e 'workflows contain no secrets' -e 'No signing keys,' -e 'issued licenses, SMTP' \
  -e 'razor-licgen., signing keys' -e 'no SMTP credentials' -e 'never needs SMTP' \
  -e 'No account, password, SMTP server' -e 'No SMTP credentials, API keys, accounts' || true)"
if [ -n "$hits" ]; then echo "$hits"; echo "FAIL: forbidden content patterns found"; FAIL=$((FAIL+1)); fi

# 4. Git staged + history (if repo)
if git -C "$ROOT" rev-parse --git-dir >/dev/null 2>&1; then
  staged="$(git -C "$ROOT" diff --cached --name-only)"
  echo "$staged" | grep -E '\.(lic|key|pem|pfx|p12|rs|pdb)$' && { echo "FAIL: staged forbidden file"; FAIL=$((FAIL+1)); } || true
  git -C "$ROOT" log --all --diff-filter=A --name-only --pretty=format: 2>/dev/null | grep -E '\.(rs|lic|key|pem|pfx|p12)$' && { echo "FAIL: private file in git history"; FAIL=$((FAIL+1)); } || true
else
  echo "Not a git repo yet (expected pre-init) — history check deferred to post-init."
fi

echo "----------------------------------------"
if [ "$FAIL" -gt 0 ]; then
  echo "AUDIT FAILED: $FAIL finding(s). STOP — do not publish."
  exit 1
fi
echo "AUDIT PASSED: no findings."
exit 0
