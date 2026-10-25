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

# 2. Allow-list gate (never committed; supplied out-of-band).
COMMERCIAL_OK=false
if [ -z "$ALLOW_LIST" ] || [ ! -f "$ALLOW_LIST" ]; then
  rec "allow-list" "SKIPPED" "no approved-binaries.txt supplied; commercial targets disabled (fail-closed)"
else
  if ! grep -q '[^[:space:]]' "$ALLOW_LIST"; then
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

# 4. Commercial staging only when authorized (hash + sidecar verified).
if [ -n "$ALLOW_LIST" ] && [ -f "$ALLOW_LIST" ]; then
  ok=1
  while IFS= read -r f; do
    f="$(printf '%s' "$f" | tr -d '[:space:]')"; [ -z "$f" ] && continue
    if [ ! -f "$ROOT/artifacts/hardened/$f" ]; then rec "stage-commercial" "FAIL" "authorized artifact missing: $f"; ok=0; fi
    if [ ! -f "$ROOT/artifacts/hardened/$f.integrity" ]; then rec "stage-commercial" "FAIL" "missing .integrity sidecar: $f"; ok=0; fi
  done < "$ALLOW_LIST"
  # SHA-256 vs sidecar check.
  while IFS= read -r f; do
    f="$(printf '%s' "$f" | tr -d '[:space:]')"; [ -z "$f" ] && continue
    h="$(sha256sum "$ROOT/artifacts/hardened/$f" | cut -d' ' -f1)"
    grep -qi "$h" "$ROOT/artifacts/hardened/$f.integrity" || { rec "stage-commercial" "FAIL" "integrity mismatch: $f"; ok=0; }
  done < "$ALLOW_LIST"
  if [ "$ok" != 1 ]; then write_manifest; exit 1; fi
  COMMERCIAL_OK=true
  rec "stage-commercial" "PASS" "authorized artifacts + sidecars verified"
else
  rec "stage-commercial" "SKIPPED" "no allow-list: commercial staging disabled"
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
