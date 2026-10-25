# BENCHMARKS

Method, scope, and what is (and is not) claimed. Machine detail and reproduction
commands live in `docs/benchmarks/METHODOLOGY.md`; per-file evidence lives in
`docs/benchmarks/*.csv`.

## Suites

### Suite 1 — verified (shipped evidence)

- **62 files, 616,890,856 bytes (616.9 MB decimal / 588.3 MiB).**
- Composition: Calgary (18) + Canterbury (11) + Silesia (12) + enwik8 (1) +
  FormalDataSet slice "formalslice" (10, local-only, hashes published for
  completeness, files NOT redistributed) + rfc9842 (10).
- Engine/tier matrix completed with **zero RT-FAILs** in the Max-tier runs the
  combined table is drawn from.
- Combined archival ratios (compressed ÷ original, lower is better):

| Engine   | Combined ratio | Combined bytes (÷ 616,890,856) |
|----------|---------------:|-------------------------------:|
| XZ       |         18.70% |                    115,327,752 |
| Zstd     |         19.86% |                    122,494,085 |
| Brotli q11 |       20.26% |                    124,959,461 |
| Razor Max  |       20.70% |                    127,694,118 |

  (Authoritative per-file evidence: `docs/benchmarks/suite1-max.csv` and the
  per-suite `suite1-<suite>-max.csv` files — the table above is derived from
  them. MaxFast comparison in `docs/benchmarks/suite1-maxfast.csv`:
  127,940,057 B → 20.74%, i.e. +245,939 B / +0.19% corpus deviation vs Max.)

- Pattern (evidence-backed): Razor Max is particularly competitive on
  structured/binary workloads; pure-text workloads such as enwik8 remain an area
  where XZ/Zstd retain an advantage.
- Outright Razor-Max file-level wins in Suite 1 include `geo`, `kennedy.xls`,
  `mr`, `ooffice`, `x-ray` (see `docs/benchmarks/ANALYSIS.md`).

### Suite 2 — broader workloads (shipped evidence where CSVs exist)

Scope (kept separate from Suite 1): enwik9 scale (2× 500 MB chunks — the 512 MiB
working-set guardrail means enwik9 is measured chunked, never as a single input),
neuro/NIfTI, E. coli genomic, NASA + GitHub logs, adversarial battery.

- Only numerical results with corresponding CSV/log evidence in `docs/benchmarks/`
  are published. No value is interpolated or manufactured.
- Each published row retains: dataset, file, engine, tier, original bytes,
  compressed bytes, ratio, encode/decode timing, round-trip result.
- Standard combined figure is labelled per-CSV; see `docs/benchmarks/ANALYSIS.md`
  for the per-dataset tables. The ~1.317 GB "standard combined" scale corpus is
  described there with its exact file list — do not quote a combined number
  without naming that list.

### What `RT-FAIL` / `RA-ENCODE-FAIL` mean

- `RT-FAIL`: compress→decompress round-trip mismatch. Recorded, never hidden.
- `RA-ENCODE-FAIL`: input above the supported working limit (or otherwise
  rejected by the encoder guardrail). Controlled termination with a recorded
  failure row — not a crash claim, not a success.
- Valid `status` values: `PASS | RT-FAIL | ERROR | TIMEOUT | SKIPPED`.

### Reproduction

1. Fetch corpora via the harness downloader (manifests in `docs/benchmarks/manifest-*.csv`
   pin URL, version/date, size, SHA-256, license, cache path).
2. Run the harness (or the CLI commands in `METHODOLOGY.md`).
3. Diff your CSV against the shipped CSVs on (file, engine, tier, sizes, ratio,
   round-trip). Timings will differ by host; ratios and pass/fail must agree.
