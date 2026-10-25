# Analysis (what the shipped CSVs show)

## Suite 1 combined (authoritative: `suite1-max.csv`)

62 files / 616,890,856 B. Zero `RT-FAIL` in the Max-tier evidence set.

| Engine | Σ compressed (B) | Ratio |
|---|---:|---:|
| XZ | 115,327,752 | 18.70% |
| Zstd | 122,494,085 | 19.86% |
| Brotli q11 | 124,959,461 | 20.26% |
| Razor Max | 127,694,118 | 20.70% |

Per-suite textures (see `suite1-<suite>-max.csv`):

- **Calgary:** Razor Max beats Brotli-q11 at suite level (26.24% vs 26.36%).
- **Canterbury:** Razor Max beats Brotli-q11 (16.81% vs 17.45%).
- **Formalslice (local-only):** Razor Max leads Brotli-q11 (15.13% vs 15.41%).
- **rfc9842:** trails Brotli-q11 slightly (+0.10 pp).
- **Silesia:** trails (+0.74 pp). **enwik8:** trails (+2.0 pp — known target).

File-level outright Razor-Max wins include: `geo`, `kennedy.xls`, `mr`,
`ooffice`, `x-ray`.

## Max vs MaxFast (documented trade-off)

MaxFast ≈ 1.6–1.8× Max encode speed with 59/62 files byte-identical in the
measured run; corpus deviation +0.19% vs Max. Known blind spot (kept in the
record, not tuned away silently): `mr` +3.0%, `x-ray` +4.1% file-residual,
plus `30mb.jpg` +0.6%. See `suite1-*-maxfast.csv` / `*-max.csv` pairs.

## Suite 2 (only where `suite2-*.csv` exists)

- **neuro** (`suite2-neuro-max.csv`): Razor Max competitive on the two NIfTI
  volumes (per-file table in CSV).
- **genomic** (`suite2-genomic-max.csv`): E. coli single-file table in CSV.
- **logs** (`suite2-logs-max.csv`): NASA + GH-Archive per-file table in CSV.
- **scale** (`suite2-scale-max.csv`): enwik9 2×500 MB chunks (512 MiB guardrail
  forces chunking); per-chunk rows in CSV.
- **adversarial** (`suite2-adv2-max.csv`, `suite2-adv-max.csv`): tiny/random/
  precompressed/repeated battery — no expansion beyond framing, 0 RT-FAIL;
  corruption battery rejected fail-closed (13/13 in the private run log;
  harness re-checks rejection on any SFX artifact under test).

No Suite 2 sentence above should be quoted without its CSV.
