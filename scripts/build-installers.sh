#!/bin/sh
# Hardened RazorZip installer build pipeline (Linux).
# Enforces the approved-binary allow-list gate; fails closed; never publishes.
# Usage: sh scripts/build-installers.sh [--allow-list /path/to/approved-binaries.txt]
set -u
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
DIST="$ROOT/dist"; STAGE="$ROOT/staging"
ALLOW_LIST="${RAZOR_ALLOWLIST_PATH:-}"
for a in "$@"; do case "$a" in --allow-list=*) ALLOW_LIST="${a#--allow-list=}";; esac; done
RESULTS=""; FAIL=0
rec() { echo "[$2] $1 :: $3"; RESULTS="$RESULTS$1|$2|$3
"; if [ "$2" = "FAIL" ]; then FAIL=$((FAIL+1)); fi; }
write_manifest() {
  { echo "{"; echo "  \"product\": \"RazorZip\", \"version\": \"0.1.0\", \"platform\": \"linux\",";
    echo "  \"commit\": \"$(git -C "$ROOT" rev-parse HEAD 2>/dev/null)\",";
    echo "  \"commercial\": ${COMMERCIAL_OK:-false},";
    echo "  \"generated_utc\": \"$(date -u +%FT%TZ)\",";
    echo "  \"results\": ["; first=1
    printf '%s' "$RESULTS" | while IFS='|' read -r t s d; do
      [ -z "$t" ] && continue
      if [ "$first" = 1 ]; then first=0; else echo ","; fi
      printf '    {"target": "%s", "status": "%s", "detail": "%s"}' "$t" "$s" "$d"
    done; echo ""; echo "  ]"; echo "}"; } > "$DIST/build-manifest-linux.json"
  echo "Manifest: dist/build-manifest-linux.json"
}
mkdir -p "$DIST" "$STAGE"

# 1. Repo audit (hard gate).
if ! sh "$ROOT/scripts/audit-public-release.sh"; then
  rec "audit" "FAIL" "source-leak audit failed; STOP"; write_manifest; exit 1
fi
rec "audit" "PASS" "source-leak/secret/history audit passed"

# 2. Allow-list gate. Default is the in-tree distribution boundary
# (approved-binaries.txt: "filename sha256 version target purpose...");
# --allow-list=/path or RAZOR_ALLOWLIST_PATH overrides it (same format).
# Hashes are cross-checked before staging; malformed lines fail closed.
COMMERCIAL_OK=false
ALLOW_TMP="$(mktemp)"; trap 'rm -f "$ALLOW_TMP" "$tmp_staged" 2>/dev/null' EXIT
if [ -z "$ALLOW_LIST" ] || [ ! -f "$ALLOW_LIST" ]; then
  if [ -f "$ROOT/approved-binaries.txt" ]; then ALLOW_LIST="$ROOT/approved-binaries.txt"; fi
fi
if [ -z "$ALLOW_LIST" ] || [ ! -f "$ALLOW_LIST" ]; then
  rec "allow-list" "SKIPPED" "no allow-list available; commercial targets disabled (fail-closed)"
else
  while IFS= read -r line; do
    case "$line" in \#*|"") continue;; esac
    n="$(printf '%s' "$line" | awk '{print $1}')"; h="$(printf '%s' "$line" | awk '{print $2}')"
    case "$n$h" in ""*|*" "* ) rec "allow-list" "FAIL" "malformed allow-list line"; write_manifest; exit 1;; esac
    printf '%s %s\n' "$n" "$h" >> "$ALLOW_TMP"
  done < "$ALLOW_LIST"
  if ! grep -q '[^[:space:]]' "$ALLOW_TMP"; then
    rec "allow-list" "FAIL" "allow-list empty/ambiguous; STOP"; write_manifest; exit 1
  fi
  rec "allow-list" "PASS" "authorized artifacts listed in $ALLOW_LIST"
fi

# 3. Stage harness (open, buildable).
if ! dotnet publish "$ROOT/harness/RazorBenchmark/RazorBenchmark/RazorBenchmark.csproj" \
  -c Release -r linux-x64 --self-contained -o "$STAGE/harness" --nologo >/dev/null 2>&1; then
  rec "stage-harness" "FAIL" "dotnet publish (linux-x64) failed"; write_manifest; exit 1
fi
cp "$ROOT/docs/trial/trial-request-template.txt" "$STAGE/harness/"
cp "$ROOT/packaging/shared/trial-mailto.txt" "$STAGE/harness/"
rec "stage-harness" "PASS" "harness published (linux-x64 self-contained) + template staged"

# 4. Commercial staging only when authorized (name+hash from parsed allow-list).
if [ ! -s "$ALLOW_TMP" ]; then
  rec "stage-commercial" "SKIPPED" "no allow-list: commercial staging disabled"
else
  ok=1
  while IFS=' ' read -r f want; do
    [ -z "$f" ] && continue
    if [ ! -f "$ROOT/artifacts/hardened/$f" ]; then rec "stage-commercial" "FAIL" "authorized artifact missing: $f"; ok=0; continue; fi
    if [ ! -f "$ROOT/artifacts/hardened/$f.integrity" ]; then rec "stage-commercial" "FAIL" "missing .integrity sidecar: $f"; ok=0; continue; fi
    got="$(sha256sum "$ROOT/artifacts/hardened/$f" | cut -d' ' -f1 | tr 'a-f' 'A-F')"
    if [ "$got" != "$want" ]; then rec "stage-commercial" "FAIL" "hash != allow-list for $f"; ok=0; continue; fi
    grep -qi "$got" "$ROOT/artifacts/hardened/$f.integrity" || { rec "stage-commercial" "FAIL" "integrity mismatch: $f"; ok=0; }
  done < "$ALLOW_TMP"
  if [ "$ok" != 1 ]; then write_manifest; exit 1; fi
  COMMERCIAL_OK=true
  rec "stage-commercial" "PASS" "authorized artifacts + sidecars verified (hash == allow-list)"
fi

# 4b. Stage commercial payload for packaging (verified above).
if [ "$COMMERCIAL_OK" = true ]; then
  mkdir -p "$STAGE/commercial"
  while IFS=' ' read -r f want; do
    [ -z "$f" ] && continue
    cp -a "$ROOT/artifacts/hardened/$f" "$ROOT/artifacts/hardened/$f.integrity" "$STAGE/commercial/"
  done < "$ALLOW_TMP"
fi

# 5. Harness selftest on staged payload.
if ! "$STAGE/harness/RazorBenchmark" --selftest >/dev/null 2>&1; then
  rec "harness-selftest" "FAIL" "staged harness --selftest failed"
else
  rec "harness-selftest" "PASS" "--selftest OK (csv round-trip, tamper-reject, trial encoding)"
fi

# 6. Debian package.
if ! command -v dpkg-deb >/dev/null 2>&1; then
  rec "debian-package" "SKIPPED" "dpkg-deb absent. Staging present: packaging/linux/deb/razorzip/"
else
  PKG="$STAGE/deb/razorzip"
  rm -rf "$PKG"; mkdir -p "$PKG"
  cp -a "$ROOT/packaging/linux/deb/razorzip/DEBIAN" "$PKG/"
  mkdir -p "$PKG/usr/lib/razorzip" "$PKG/usr/bin" "$PKG/usr/share/applications" "$PKG/usr/share/doc/razorzip"
  cp -a "$STAGE/harness" "$PKG/usr/lib/razorzip/"
  ln -sf /usr/lib/razorzip/harness/RazorBenchmark "$PKG/usr/bin/razor-benchmark"
  cp "$ROOT/packaging/linux/deb/razorzip/usr/share/applications/razorzip-trial.desktop" "$PKG/usr/share/applications/"
  cp "$ROOT/docs/trial/trial-request-template.txt" "$PKG/usr/share/doc/razorzip/"
  if [ "$COMMERCIAL_OK" = true ]; then
    # Allow-listed engine payload (hash-verified in step 4).
    mkdir -p "$PKG/usr/lib/razorzip/engine"
    while IFS=' ' read -r f want; do
      [ -z "$f" ] && continue
      cp -a "$STAGE/commercial/$f" "$STAGE/commercial/$f.integrity" "$PKG/usr/lib/razorzip/engine/"
    done < "$ALLOW_TMP"
  fi
  chmod 755 "$PKG/DEBIAN/postinst" "$PKG/DEBIAN/prerm" "$PKG/DEBIAN/postrm"
  # No broad permissions: program files root-owned 755/644 via dpkg defaults.
  if dpkg-deb --build "$PKG" "$DIST/razorzip-harness-0.1.0-amd64.deb" >/dev/null 2>&1; then
    rec "debian-package" "PASS" "dist/razorzip-harness-0.1.0-amd64.deb built (harness-only)"
    debhash="$(sha256sum "$DIST/razorzip-harness-0.1.0-amd64.deb" | cut -d' ' -f1)"
    rec "artifact" "INFO" "razorzip-harness-0.1.0-amd64.deb sha256=$debhash"
  else
    rec "debian-package" "FAIL" "dpkg-deb build failed"
  fi
fi

# 7. RPM package.
if ! command -v rpmbuild >/dev/null 2>&1; then
  rec "rpm-package" "SKIPPED" "rpmbuild absent. Spec present: packaging/linux/rpm/razor-enterprise.spec"
else
  rec "rpm-package" "SKIPPED" "rpmbuild present but SOURCES/payload wiring is manual in this release; see spec header"
fi

# 8. Manifest + exit code (SKIPPED is not failure; FAIL is).
write_manifest
if [ "$FAIL" -gt 0 ]; then exit 1; fi
exit 0
