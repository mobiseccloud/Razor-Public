# CHANGELOG

All notable changes to this **public** repository. Dates are UTC. This log covers
only public material (harness, docs, evidence, scripts); proprietary engine
history is not published here.

## [Unreleased]

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
