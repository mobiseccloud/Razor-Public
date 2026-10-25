# SUPPORT

## What to ask where

| Topic | Channel |
|---|---|
| Harness build/run, CSV schema, corpus manifests | This repo's issue tracker (`https://github.com/mobiseccloud/Razor-Public/issues`, include CSV + manifest + versions) |
| Benchmark discrepancy | Issue tracker with: your CSV, shipped CSV diff, engine versions, host/threads, settings |
| Commercial trial / licensing / pricing | `razor@mobisec.cloud` (see `docs/trial/`) |
| Security-sensitive report | `razor@mobisec.cloud`, subject `[SECURITY] <summary>` (see `SECURITY.md`) |

## Response expectations

- Public harness/docs issues: triaged on a best-effort basis; benchmark
  reproducibility issues get priority when they include full artifacts.
- Commercial/trial mail: include the structured trial template from
  `docs/trial/REQUEST-TEMPLATE.md` for trial requests. Requests are reviewed
  manually; one-trial-per-address enforcement, if any, happens in that manual
  review (no automatic issuance service exists in this release).

## Before filing a benchmark issue

1. Re-run with the manifest-pinned corpus (SHA-256 verified).
2. Attach your exported CSV and the shipped CSV you compared against.
3. State engine binaries + versions, host, thread count, tier.
4. Note that timings move with load — compare ratios and pass/fail, not ms.
