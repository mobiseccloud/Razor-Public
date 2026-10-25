# Methodology (how the numbers were produced)

## Runner model

Each (file × engine × tier) cell: compress to a temp output → record compressed
bytes + encode wall-clock → decompress → byte-compare with the original →
record decode wall-clock + round-trip verdict. Any mismatch → `RT-FAIL`.

## Suite 1 provenance

- Runs: licensed Max-tier engine runs, 2026-10-24, single-threaded control where
  noted (`max-*` CSVs). Raw private CSVs (columns: file, orig, rs/ra + `_cms/_dms`
  timings, competitor sizes) were converted to the public schema without altering
  any size/ratio/round-trip value. Timing columns map: `_cms` → `encode_ms`,
  `_dms` → `decode_ms`; MB/s derived as `bytes / (ms/1000) / 1e6`.
- Conversion script: `scripts/convert-suite1.ps1` (kept out of the public tree —
  run on the private estate; the shipped CSVs are its output, re-verifiable from
  the per-file sizes below).
- Totals check: Σ original = 616,890,856 B; Σ Razor-Max compressed = 127,694,118 B
  → 20.6999…% ≈ **20.70%**. Competitor columns recompute to XZ 18.70%,
  Zstd 19.86%, Brotli-q11 20.26%.

## Host/threads/version fields

Public CSV rows carry `host`, `threads`, `binary_version`, `format_version` from
the measuring run. Absolute ms values are host-specific; ratios and pass/fail are
the portable claims.

## Reproduce with the harness

1. Download corpora via manifest (SHA-256 verified).
2. Select engines (Razor binary optional; third-party baselines required for the
   comparison columns).
3. Run matrix → export CSV → compare (file, engine, tier, sizes, ratio, status).

## What is NOT claimed

- No multi-thread scaling claim beyond the recorded `threads` field.
- No claim that timings transfer across machines.
- No Suite 2 combined claim beyond the exact file list in `ANALYSIS.md`.
