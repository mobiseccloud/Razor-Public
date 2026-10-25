# INSTALL — GUI and CLI installation options

## Windows

### Wizard (Inno Setup, recommended for interactive installs)

1. Build or obtain the setup exe (harness-only unless allow-list-gated):
   `powershell ./scripts/build-installers.ps1` (needs Inno Setup 6 `iscc.exe`
   on PATH, else the target records SKIPPED with the reason).
2. Run `dist/RazorZip-*-setup.exe`. Default location
   `C:\Program Files\MobiSec\RazorZip`; the privileges dialog offers a
   per-user installation.
3. Component choice: **Full** (recommended), **harness only**, or **CLI only**.
   Commercial engine components appear only in gated builds.
4. The **Request Your 14-Day RazorZip Enterprise Trial** page explains that a
   request is by email and the license arrives separately after review.
   **Back/Next/Finish** follow the normal flow; the email buttons never block
   installation. In silent installs (`/SILENT`, `/VERYSILENT`) the page is
   skipped — request afterwards from the GUI or CLI.
5. Start Menu: **Razor Benchmark Harness** and **Request 14-Day Trial**
   (the latter launches the harness with `--request-trial`).

### MSI alternative — REMOVED as a deliverable

There is no MSI package. Windows delivery is Inno Setup only
(`packaging/windows/razorzip-setup.iss`), which covers silent installs
(`/SILENT`, `/VERYSILENT` skip the trial page; use the GUI Trial tab or
`--request-trial` afterwards). The former WiX source was removed because its
payload fragments were never generated and it was never compilable.

## Linux

### Debian

1. `sh scripts/build-installers.sh` (needs `dpkg-deb`, else SKIPPED recorded).
2. `sudo dpkg -i dist/razorzip-harness-0.1.0-amd64.deb`.
3. Files: `/usr/lib/razorzip/harness/`, link `/usr/bin/razor-benchmark`,
   desktop entry `razorzip-trial.desktop`, docs under
   `/usr/share/doc/razorzip/`. `postinst` only refreshes the desktop database —
   no email, no license creation, no service contact.

### RPM

- Spec: `packaging/linux/rpm/razor-enterprise.spec`. `rpmbuild` wiring for
  SOURCES/payload is manual in this release (recorded as such by the script).
- The `razor-watch.service` unit ships **disabled** and is never enabled or
  started by `%post`; administrators opt in explicitly.

## CLI after any install

- `RazorBenchmark --request-trial` — graphical: opens email handler;
  headless/SSH: prints recipient + subject + template + instructions.
- `RazorBenchmark --copy-trial-template` — prints the template text.
- `RazorBenchmark --selftest` — CSV round-trip, SHA-256 tamper-reject, and
  trial-email encoding checks (CI-safe, no client launched).
- `RazorBenchmark --engines` — engine availability probe.

## After installing a returned trial license

Follow the issuance email for placement (authoritative). See
`docs/security/LICENSE-DIRS.md` — the proposed `%PROGRAMDATA%` / `/var/lib`
paths are UNVERIFIED against the private loader and are not provisioned by
any package here. Confirm acceptance via the engine's own license-status
reporting.
