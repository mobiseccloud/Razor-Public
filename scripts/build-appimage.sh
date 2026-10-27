#!/bin/sh
# Build the RazorZip benchmark-harness AppImage (Linux, x86_64).
# Standalone: dotnet publish (self-contained) -> AppDir -> appimagetool.
# Output: dist/RazorZip-<version>-x86_64.AppImage + .sha256 (dist/ is ignored).
# No commercial engine payload: harness + trial template only.
set -u
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
VERSION="${RAZORZIP_VERSION:-0.1.0}"
DIST="$ROOT/dist"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
APPDIR="$WORK/RazorZip.AppDir"
APPIMAGETOOL="${APPIMAGETOOL:-$(command -v appimagetool || true)}"

if [ -z "$APPIMAGETOOL" ]; then
  echo "SKIP: appimagetool not found (set APPIMAGETOOL=/path/to/appimagetool)"
  exit 3
fi
if ! command -v dotnet >/dev/null 2>&1; then
  echo "SKIP: dotnet SDK absent"
  exit 3
fi

# 1. Self-contained Linux publish (same flags as build-installers.sh).
PUB="$WORK/harness"
dotnet publish "$ROOT/harness/RazorBenchmark/RazorBenchmark/RazorBenchmark.csproj" \
  -c Release -r linux-x64 --self-contained -o "$PUB" --nologo || exit 1
find "$PUB" -name '*.pdb' -delete
cp "$ROOT/docs/trial/trial-request-template.txt" "$PUB/"
test -x "$PUB/RazorBenchmark" || { echo "FAIL: apphost missing after publish"; exit 1; }

# 2. AppDir layout.
mkdir -p "$APPDIR/usr/lib/razorzip" "$APPDIR/usr/share/applications" \
  "$APPDIR/usr/share/icons/hicolor/256x256/apps"
cp -a "$PUB" "$APPDIR/usr/lib/razorzip/harness"
mkdir -p "$APPDIR/usr/bin"
ln -sf ../lib/razorzip/harness/RazorBenchmark "$APPDIR/usr/bin/razor-benchmark"
cp "$ROOT/assets/icon_primary.jpg" \
  "$APPDIR/usr/share/icons/hicolor/256x256/apps/razorzip.jpg"
cp "$ROOT/assets/icon_primary.jpg" "$APPDIR/.DirIcon"
# AppImage needs a PNG icon at the AppDir root (brand PNGs are an owner
# decision — generate from the approved JPG at build time instead).
if ! python3 -c "import PIL" 2>/dev/null; then
  echo "SKIP: python3+Pillow required for icon conversion (pip install pillow)"
  exit 3
fi
python3 - "$ROOT/assets/icon_primary.jpg" "$APPDIR" <<'PYEOF'
import sys
from PIL import Image
src, appdir = sys.argv[1], sys.argv[2]
img = Image.open(src).convert("RGB").resize((256, 256))
img.save(appdir + "/razorzip.png")
img.save(appdir + "/usr/share/icons/hicolor/256x256/apps/razorzip.png")
img.save(appdir + "/.DirIcon.png")
PYEOF
cat > "$APPDIR/razorzip.desktop" <<'EOF'
[Desktop Entry]
Name=RazorZip Benchmark
Comment=Open benchmark harness for the Razor compression engine (Core features)
Exec=RazorBenchmark
Icon=razorzip
Type=Application
Categories=Utility;System;
Terminal=false
StartupWMClass=RazorBenchmark
EOF
cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/lib/razorzip/harness/RazorBenchmark" "$@"
EOF
chmod +x "$APPDIR/AppRun"

# 3. Build.
mkdir -p "$DIST"
OUT="$DIST/RazorZip-$VERSION-x86_64.AppImage"
rm -f "$OUT"
ARCH=x86_64 "$APPIMAGETOOL" "$APPDIR" "$OUT" || exit 1
sha256sum "$OUT" | cut -d' ' -f1 > "$OUT.sha256"
echo "BUILT: $OUT ($(stat -c%s "$OUT") bytes, sha256=$(cat "$OUT.sha256"))"
