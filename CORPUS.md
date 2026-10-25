# CORPUS

Corpus files themselves are **not** redistributed in this repository (unless their
license explicitly permits it — none are vendored here). The harness downloads from
official/public sources and verifies SHA-256 per the manifests in
`docs/benchmarks/`.

## Suite 1 (verified combined table)

| Suite part | Files | Size (bytes) | Provenance / license | Manifest |
|---|---|---:|---|---|
| Calgary | 18 | 3,141,622 | Public domain-ish classic corpus (Witten & Bell). Download from official mirrors; hashes in manifest. | `docs/benchmarks/manifest-suite1.csv` |
| Canterbury | 11 | 2,810,784 | University of Canterbury corpus (Ross Arnold). Official site mirrors. | `docs/benchmarks/manifest-suite1.csv` |
| Silesia | 12 | 211,864,096 | Silesia corpus (Deorowicz). Research use; official distribution. | `docs/benchmarks/manifest-suite1.csv` |
| enwik8 | 1 | 100,000,000 | Hutter Prize (Wikipedia prefix). Public. | `docs/benchmarks/manifest-suite1.csv` |
| rfc9842 | 10 | ~505 kB total | IETF RFC 9842 artifacts + tooling outputs. Public. | `docs/benchmarks/manifest-suite1.csv` |
| formalslice | 10 | ~292 MB | **Local-only.** Maintainer-estate slice (pre-compressed-media heavy). Hashes published for completeness; files are NOT downloadable from here and are NOT required to reproduce the public suites. | `docs/benchmarks/manifest-suite1.csv` (marked `local-only`) |

Total: **62 files / 616,890,856 bytes.**

## Suite 2 (broader workloads)

| Dataset | Content | License / source |
|---|---|---|
| enwik9 scale | 2× 500 MB chunks (`enwik9-part1/2-of-2.bin`) | Hutter Prize enwik9, chunked locally because of the 512 MiB guardrail. Chunk hashes in `manifest-suite2.csv`. |
| neuro | NIfTI `asl16.nii` (19 MB), `asl32.nii` (38 MB) | Source-scanned neuroimaging samples; see manifest provenance field. |
| genomic | `E. coli` (4.6 MB) | Public reference genome; see manifest. |
| logs | NASA Jul-95 access log (205 MB), GH-Archive JSON hour (50 MB) | Public logs; see manifest. |
| adversarial | tiny / random / precompressed / repeated-pattern battery | Generated; generator note in manifest. |

All Suite 2 rows shipped here have CSV evidence (`docs/benchmarks/suite2-*.csv`).
Anything without a CSV is not numerically claimed.

## Downloader contract (harness)

Each manifest entry: source URL, version/release date where available, expected
size, SHA-256, license/provenance, local cache location. The downloader shows
progress %, bytes downloaded, retry, failure reporting, SHA-256 verification,
mismatch rejection. Corruption is never silently accepted.
