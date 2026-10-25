# FORMAT (public disclosure subset)

This note discloses only what a benchmark consumer needs to interpret results.
It is **not** a codec specification and intentionally omits proprietary internals
(matchers, entropy cascade, lens library, training, profiles).

## Container orientation

- Razor archival outputs are whole-file artifacts with per-block integrity fields.
- Every block carries a length and a checksum covering the block payload as stored;
  the file carries an end-to-end digest verified on decode.
- Streaming tiers use small blocks (low latency); archival tiers use large blocks
  (ratio). Tiers are strict subsets for size purposes: larger tiers can only win
  against smaller ones on the same input in the reference configuration.

## Integrity model (what the harness checks)

1. Per-block checksum failure → block rejected, run marked `RT-FAIL`/`ERROR`
   (never a silent success).
2. End-to-end digest mismatch → whole output rejected.
3. Truncated / tampered inputs → controlled, fail-closed rejection.

## 512 MiB working-set guardrail

- Supported working limit: **512 MiB per input**.
- `INPUT > SUPPORTED WORKING LIMIT → RA-ENCODE-FAIL → CONTROLLED TERMINATION.`
- This is a deliberate defensive guardrail, not a crash: the run is recorded as a
  failure row and the process exits cleanly.
- Distinguish: (a) codec working-set limit, (b) logical file size,
  (c) future large-file chunking/container support. No chunking/container support
  is claimed until a tested implementation exists. Enwik9-scale inputs are
  therefore measured as 500 MB chunks (see Suite 2).

## Versioning

- `binary_version` and `format_version` are recorded per CSV row so results stay
  attributable when the commercial engine evolves.
- The harness makes no claim about wire-format stability beyond what the CSVs'
  version fields state.
