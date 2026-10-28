# CHANGELOG

All notable changes to this **public** repository. Dates are UTC. This log covers
only public material (harness, docs, evidence, scripts); proprietary engine
history is not published here.

## [Unreleased]

- One-click benchmark flow (harness + headless): `RunMatrixAsync` runs
  verified cached Suite 1 files through every available engine as a
  cancellable background task (GUI Run button) or via `--run <manifest>
  --out <csv>`; `--fetch-manifest` provisions the corpus the same way the
  GUI Download button does. Unavailable engines count as SKIPPED, never
  fabricated; only measured PASS/RT-FAIL cells become CSV rows (see
  METHODOLOGY). Commercial engine paths resolve via
  `RAZORBENCH_ENGINE_<NAME>_<TIER>` or stay SKIPPED. Selftest now also
  covers manifest parsing, downloader verification, matrix fakes
  (PASS/RT-FAIL/SKIPPED/TIMEOUT), and comparison math.
- Quarantine reason resolved: small-input defect fixed (private checkpoint
  `533cc6b`); fresh builds re-passed the full gate battery (see
  `artifacts/hardened/MANIFEST.txt`). Distribution still awaits the
  protector wrap — bench/watch/decode remain undistributed by policy.
- License-loader paths verified per product
  (`docs/security/LICENSE-LOADER-PATHS.md`); old drop-in directories retired.
- AppImage pipeline added (`scripts/build-appimage.sh`, wired into
  `build-installers.sh` as a gated step); first artifact smoke-tested
  (`--selftest`/`--engines` green under WSL, clean-machine run still open).
- First metric traces (`docs/benchmarks/metric-trace-20261027.csv`):
  decompression 166 MB/s, log reclaim 91.34%, Rayon 2.15x (calgary),
  early-exit proxy 5.39x; trainer gain unmeasured (no such mechanism).
- Shipped first verified commercial binary: `razor-train.exe` + integrity
  sidecar (allow-listed, hash-verified, smoke/functional/tamper gates passed;
  see `artifacts/hardened/MANIFEST.txt`).
- Quarantined `razor-bench.exe`, `razor-watch.exe`, sfx-decode tooling after
  deterministic small-input round-trip/verify-decode failures in this build
  (fail-closed; evidence corpora unaffected). Removed from the allow-list;
  re-ship requires a fixed build + full gate battery.
- Removed WiX/MSI as a deliverable (unbuildable scaffolding; Inno Setup only).

- Hardened deployment packaging (scaffolding, harness-only until allow-listed):
  Inno Setup wizard with trial-email page, WiX MSI alternative, Debian staging,
  RPM spec, Linux desktop trial entry, gated `build-installers.ps1/.sh`.
- One-click trial workflow: GUI Trial tab, `--request-trial` /
  `--copy-trial-template` CLI, installer buttons, desktop entry — all open the
  user's own email client prefilled (mailto, UTF-8 percent-encoded) or fall
  back to copy/print. Never auto-sends; no credentials embedded. Template
  source of truth: `docs/trial/trial-request-template.txt`.
- License directories documented as PROPOSED/UNVERIFIED
  (`docs/security/LICENSE-DIRS.md`); installers provision none.

- Initial public Razor / RazorZip release staging (this change).
- Harness: Avalonia (.NET 10) open benchmark GUI.
- Evidence: Suite 1 verified CSVs (62 files / 616.9 MB, zero RT-FAILs in the
  Max-tier matrix) + Suite 2 CSVs where evidence exists.
- Docs: README, BENCHMARKS, CORPUS, FORMAT (disclosure subset), ARCHITECTURE,
  SECURITY, SUPPORT, trial workflow.
- Scripts: `audit-public-release.ps1/.sh`, `verify-release.ps1`; CI workflow.
- Commercial binaries: intentionally absent — no `approved-binaries.txt`
  allow-list was provided, so the binary gate held (see
  `artifacts/hardened/README.md`).
