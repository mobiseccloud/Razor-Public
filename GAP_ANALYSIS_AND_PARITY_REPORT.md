# GAP ANALYSIS AND PARITY REPORT — Razor public release surface

Date: 2026-10-26. Scope: `Razor` (private reference), `Razor-public-release`
(public tree @ `5a863d0`, tag `razorzip-0.1.0`, pushed), `razor-licensing-service`
(@ `9d7073f`, deployed to Coolify, issuance disabled), plus the commercial
executive brief. Method: source reads, both audit scripts, binary string
scans, runtime CLI checks, license-matrix tests (unit + live). No pushes,
no deployments, no rebuilds performed for this report.

## 0. Headline verdicts (deviations from the audit directive are deliberate)

1. **Windows delivery stays Inno Setup only. WiX/MSI is NOT being
   reintroduced.** The directive's "WiX / MSI / Wizard" phrasing conflicts
   with the owner's explicit, committed decision (9212683 removed WiX as
   unbuildable scaffolding; pipeline records RETIRED). Re-adding WiX would
   regress the release. The Inno wizard covers GUI + CLI + silent install.
2. **No proprietary leak found** by source audit (both scripts PASS),
   binary string scan (setup.exe + razor-train.exe CLEAN), or tracked-file
   review — with one **deliberate, documented exception** (§2).
3. **"100% complete GUI / 100% certainty" are not attainable claims.**
   The GUI is intentionally partial (documented in code + QUICKSTART); the
   audit reports methodology + findings, not mathematical certainty.
4. **GitHub issues could NOT be filed**: `gh` is installed but unauthenticated
   (no token available; interactive login is an owner action). Draft bodies
   are included below (§7), ready to paste.

## 1. Feature parity & deviation matrix

| # | Capability (brief) | Private `Razor` | Public tree | Licensing service | Status |
|---|---|---|---|---|---|
| 1 | Archival engine (Max/Balanced/Fast/MaxFast/UltraFast) | ✅ tournament + tiers | ✅ measured as black-box; CSVs shipped | n/a | PARITY |
| 2 | Streaming + bounded memory | ✅ `compress_stream` | ✅ harness exercises stream cols | n/a | PARITY |
| 3 | Log-archiver service | ✅ `razor-watch` (+ `--install`, systemd unit) | ⚠️ unit scaffold only; binary defect-fixed + verified 2026-10-27 but undistributed (protector wrap pending) | n/a | **GAP — distribution blocked on protector tooling** |
| 4 | SFX + spanning + AES-256 + JWT/OTP auth | ✅ make/stub flags + auth.rs | ⚠️ round-trip + spanning + auth gates verified 2026-10-27 on qualified builds; not distributed | n/a | **GAP — same blocker** |
| 5 | `--stdout/--file/--quiet` zero-disk | ✅ stub, Pro-gated | ❌ not in any installer (no qualified binary); zero-tmp verified | n/a | **GAP — same blocker** |
| 6 | `razor-train.exe` → `.rzprofile` | ✅ | ✅ allow-listed, bundled, hash-verified | n/a | PARITY |
| 7 | Tier licensing + 14-day trial | ✅ `razor-license` gates | ✅ request flow + loader docs | ✅ intake+review live; issuance disabled | PARITY (manual fulfilment) |
| 8 | Podium benchmark vs corpora | ✅ `razor-bench` + corpus/ | ✅ harness `--engines/--csvcheck/--selftest`; evidence CSVs | n/a | PARTIAL — harness has **no Run button** (documented); click-run bench exe qualified but undistributed (same blocker) |
| 9 | AppImage (Linux) | n/a (not built anywhere) | ✅ pipeline added 2026-10-27 (`scripts/build-appimage.sh`, gated step in `build-installers.sh`); artifact built + smoke green under WSL | n/a | PARTIAL — clean-machine run still open |
| 10 | Browser/shell extensions | ❌ do not exist in ANY tree | ❌ | n/a | NOT APPLICABLE — no such components exist to verify |
| 11 | Hybrid levels | private research (`experiment/hybrid-20261026-neutral`, NEUTRAL) | ❌ correctly absent | n/a | PARITY (kept out of launch by decision) |
| 12 | Installer license dirs/placement | loader: flag→env→exe-adjacent | ✅ verified `LICENSE-LOADER-PATHS.md` (old `%PROGRAMDATA%` paths retired) | ✅ license email states placement + pubkey | PARITY |

## 2. Proprietary leak audit

- `audit-public-release.ps1` + `.sh`: **PASS** (3 prior findings from the new
  loader docs resolved via file-scoped benign exceptions with written
  rationale + exact-filename rewording; no patterns weakened).
- Binary string scan (scanner key/secret/credential marker set):
  setup.exe **CLEAN**, razor-train.exe **CLEAN**.
- `git ls-files`: **zero** `.exe/.deb/.rpm/.msi/AppImage` under `dist/` or
  `staging/` (both ignored). **One deliberate exception**:
  `artifacts/hardened/razor-train.exe` (+88 B sidecar) is tracked by owner
  decision (8d401cc) as the allow-listed, hash-pinned commercial payload.
  Recommendation: keep (documented) or move to Releases-only — owner call.
- No `.rs`/keys/secrets/signing material anywhere public. Internal telemetry:
  none in public tree (harness reports local CSV stats only).

## 3. GUI / CLI verification (today)

- `--selftest` PASS (CSV round-trip, tamper-reject, trial encoding).
- `--engines` clean probe output (honest not-configured/unavailable states).
- `--csvcheck suite1-max.csv`: OK, 372 rows.
- GUI: single `MainWindow` (Trial tab + Dashboard CSV view wired; Run,
  Download, Settings **not implemented** — self-declared in
  `MainViewModel.cs:29` and QUICKSTART). Trial-tab click-through remains
  untest-executed (open acceptance item).
- Edge cases (tiny/corrupt/truncated): decoder binaries quarantined, so no
  untested decoder ships; harness tamper-reject path covered by selftest;
  C# decompress negatives fail-closed (exit 2), verified 2026-10-26.
- AppImage: no pipeline, no tooling, no artifact. Building one requires:
  self-contained Linux publish (proven) + `appimagetool` provisioning +
  desktop-file/icons already present + smoke test. Estimate: half-day task.

## 4. Installers (alignment held)

- Windows: `razorzip-setup.iss` (Inno 6+; `{commonpf}`), allow-list-gated
  `/DCOMMERCIAL`, silent install/uninstall verified on dev host, quarantine
  excluded by construction (unreferenced binaries fail the compile).
- Linux: `build-installers.sh` end-to-end (deb + rpm), install/remove +
  selftest verified in WSL; ICU deps declared; service unit ships disabled.
- CI (`.github/workflows/ci.yml`) runs audit + verify + selftest + Linux
  harness build; installer compilation is local-only (needs Inno + hardened
  binaries) — by design, since release assets publish via GitHub Releases.
- Release-asset rule holds: `dist/` + `staging/` ignored, hashes in
  manifests, `razorzip-0.1.0` tag pushed; 0.1.0 release upload is owner action.

## 5. Hardening & license gates (matrix)

| Gate | Evidence | Confidence |
|---|---|---|
| Hardened binaries + sidecars, hash match | manifests + re-verified SHA-256 (setup `04A2B3C9…`, train `5FE23D…`) | HIGH |
| Tamper-negative (corrupt sidecar → silent exit 1) | pipeline gate, prior run | MEDIUM (not re-run today) |
| Community fallback (no license) | bench `Ok(None)` → Community; every Community run | HIGH |
| Trial unlock (enterprise + pubkey → Max) | runtime: Max runs, RT clean | HIGH |
| Missing license on gated tier | runtime: exit 1 naming exact path tried | HIGH |
| Expired / tampered / fingerprint / denylist | private unit tests (`expired_rejected`, `tampered…`, `revoked_rejected`, …) | HIGH (unit), MEDIUM (no live denylist drill) |
| Watcher/SFX gates | source-verified (`enforce_capability`, hard Enterprise fail) | MEDIUM — runtime blocked by quarantine |

## 6. Brief claims vs evidence (must reconcile before tester-facing use)

Verified: Suite-1 table (Max 20.70% combined; enwik8 +2.0pp gap disclosed);
trial unlock; quarantine boundaries. **Update 2026-10-27:** first traces in
`docs/benchmarks/metric-calgary-max-{t1,auto}.csv` and
`metric-incomp-max{,-fast}.csv` (standard schema, checkpoint-built binaries)
— decompression 166 MB/s from Suite-1 totals (inside 85–264+ band), log
reclaim 91.34% from suite2-logs totals (inside 85–92%), Rayon 2.15x on
calgary (BELOW 3.38–4.14x: different workload/host, claim untraced on
large workloads), early-exit proxy 5.39x (exceeds 4.2x on incompressible
slice), trainer 2–5% UNMEASURED (no framing-weight mechanism; profiles steer
tier recommendation only). Do not ship untraced figures in tester material.

## 7. GitHub issues (filed 2026-10-26 as #1–#5; bodies below for the record)

### Issue 1 — AppImage deliverable missing [enhancement]
Body: No AppImage pipeline, tooling, or artifact exists for the Linux
harness despite click-run demand. Acceptance: `appimagetool` provisioned,
self-contained publish bundled, desktop entry/icons reused, smoke test
(`--selftest` + `--engines`) green from the mounted AppImage on a clean
Ubuntu/Fedora host. Repro: `find . -iname '*appimage*'` → empty.

### Issue 2 — GUI Run/Download/Settings unwired [enhancement]
Body: `MainViewModel.cs:29` + QUICKSTART declare benchmark Run, corpus
download, and engine setup unimplemented (`DownloaderService`,
`BenchmarkService`, `EngineRunner` are unexercised). Acceptance: wire or
explicitly remove; until then the Trial tab + CSV view remain the supported
surface. Repro: launch GUI, observe absent controls.

### Issue 3 — Trial-tab click-through untested [testing]
Body: URI construction is unit-covered (`--selftest`) but no interactive
click-through evidence exists. Acceptance: recorded click test on Windows
(+ Linux desktop entry) showing the prefilled mailto / clipboard fallback.

### Issue 4 — Watcher/SFX runtime qualification blocked [bug, blocker]
Body: `razor-bench`/`razor-watch`/sfx-decode quarantined after deterministic
small-input RT-FAILs (`MANIFEST.txt`). Acceptance: fixed rebuild passes the
full gate battery and is re-allow-listed. Blocks items 3–5 of the feature
matrix and any watcher-including installer.

### Issue 5 — Marketing figures need traced measurements [docs]
Body: brief figures (§6 above) have no measurement trace in `docs/benchmarks/`.
Acceptance: per-claim runs added to evidence, or wording downgraded to
aspirational. Repro: grep CSVs for the claimed bands.

## 8. Release-script readiness

`build-installers.ps1/.sh` present, gated (allow-list, sidecars, fail-closed
appends), manifests hash-recorded; CI green path verified today
(audit×2 + verify + CLI checks). Next release build exercises them unchanged.
Uncommitted working tree from this audit: this report (new, untracked),
`LICENSE-LOADER-PATHS.md` (new), `LICENSE-DIRS.md` + both audit scripts
(modified, exceptions only) — commit decision is the owner's.

## 9. Execution addendum 2026-10-27 (master directive Tasks 1–6)

### Task 1 — unquarantine: defect FIXED + verified, distribution BLOCKED
- Clean worktree at private `533cc6b` (+3 git-ignored SFX/bin sources copied
  for the build only); C: disk was full (0 B free) so all target dirs went
  to `D:/Temp/rz-target` (reported, removable).
- Full battery green: tiny-input regression test; Rust suites 41+1 / 19 /
  39 / 8 / 8 / 6; C# solution 0 errors; C# streaming 33/33 + archival 33/33;
  6 negatives fail-closed (exit 2; oversize-claim leaves a 500 B partial —
  pre-existing deviation, exit code correct).
- Fresh `--profile dist` + `RAZOR_GUARD_ENFORCE=1` builds: bench, watch,
  sfx-decode/make/stub + rzsign. Sidecars generated + verified; tamper
  negative re-verified (corrupt sidecar -> silent exit 1; audit-mode builds
  do NOT enforce — enforcement requires the ENFORCE=1 build as the pipeline
  does).
- Watcher small-log end-to-end (40/63/100/200/500 B): 6/6 archived, zero
  failure signatures; outputs decode byte-identical. The 40 B skip was
  rotation-safety (newest-file rule), confirmed via `--include-active`.
- **Blocker found by the pipeline itself**: `stage-commercial` failed —
  rzsign emits section-hash (`text`) sidecars for unpacked binaries while
  the gate requires full-file-hash (`full`) sidecars, which only packed
  (protector-wrapped) binaries produce. The protector tool is absent on
  this machine, so the wrap cannot run here. Rather than weaken the check,
  the distribution changes were reverted: allow-list/MANIFEST/installer
  restored to train-only, quarantine reason updated from "defect" to
  "hardening delta". Qualified hashes are recorded in MANIFEST.txt.
  Required next: protector pass on a provisioned machine, then re-hash.

### Task 2 — AppImage: DONE (provisional)
- `scripts/build-appimage.sh` (new) + gated step in `build-installers.sh`;
  artifact `RazorZip-0.1.0-x86_64.AppImage` (41.8 MB) built, `--selftest`
  and `--engines` green under WSL (FUSE present). Clean-machine run open.
  Icon PNGs are generated at build time (python3/Pillow) — no brand asset
  committed; appimagetool itself is provisioned per-machine (not in repo).

### Task 3 — GUI/trial: DONE (within scope)
- No dead buttons: all 4 XAML buttons map to implemented commands with
  fallbacks; missing Run/Download/Settings are absent features, not dead
  controls (Issue #2 stays open as enhancement).
- Trial chain byte-verified: desktop Exec == shared mailto; decoded body
  contains the canonical template; TrialEmail constants + length guard +
  clipboard fallbacks reviewed. Interactive click stays clean-VM-only.
  (Directive's `licensing@mobisec.cloud` appears nowhere — canonical
  `razor@mobisec.cloud` held.)

### Task 4 — enterprise suite: verified as far as shippable binaries allow
- Watch: rotation detection, newest-file safety, purge via explicit flag,
  no-crash gates. **New defect found**: config-file `auto_purge` does not
  purge (explicit `--retention delete` works; unit test bypasses config
  parsing) — file as new issue; test-hygiene note: state files default to
  CWD, keep test dirs isolated.
- SFX: 6-volume spanning continuous + byte-identical; password
  right/wrong/missing fail closed; token-required halts without token.
  Operational rule discovered: SFX binaries self-verify sidecars, so the
  pipeline must re-sign AFTER `make` (renamed/appended copies fail silent
  exit 1 — correct secure behavior, plus a PowerShell stdout-capture
  artifact to avoid when measuring).
- Train: emits valid `.rzprofile` (tier-recommendation mechanism).
  **Deviation**: no entropy-framing-weight injection exists — the brief's
  "2–5% via framing weights" has no code path; profiles steer tier choice.

### Task 5 — metric traces: DONE (evidence committed)
- `docs/benchmarks/metric-calgary-max-{t1,auto}.csv`,
  `metric-incomp-max{,-fast}.csv` (standard schema, checkpoint-built
  binaries; translator refuses failing rows): Rayon 2.15x byte-identical,
  early-exit proxy 5.39x at +0.18% deviation, zero failures everywhere.
  Decompression 166 MB/s + log reclaim 91.34% traced from Suite totals.
  Trainer gain: UNMEASURED (no mechanism).

### Task 6 — this commit (no push)
- Audits PASS ×2, verify PASSED, no tracked binaries beyond the deliberate
  `razor-train.exe` exception. Commit covers: Task 1 revert-state files,
  AppImage script + installer step, metric CSVs, CHANGELOG, this report.
  Push + tag decisions remain the owner's.
