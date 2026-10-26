# License-loader paths — authoritative per-product reference

Status: **VERIFIED** (2026-10-26) against the proprietary loader source
(`razor-license` crate) and runtime path-discovery tests with throwaway
licenses. This document replaces the PROPOSED paths in `LICENSE-DIRS.md`
where they conflict: the engine does **not** read `%PROGRAMDATA%`,
`/var/lib/razor/licenses/`, or any fixed system directory.

## Resolution order (all licensed CLI products)

Implemented once in `razor_license::resolve_license_path`, shared by every
licensed binary. Verified at runtime (precedence matrix: flag beats env,
env beats exe-adjacent, exe-adjacent beats CWD fallback):

1. Explicit `--license <path>` flag (highest precedence).
2. `RAZOR_LICENSE` environment variable (a file path; empty/blank ignored).
3. `razor.lic` next to the current executable (`current_exe` parent dir).
4. Fallback: relative `razor.lic` (current working directory).

The vendor public key resolves independently: `--license-pubkey <hex>` flag,
then `RAZOR_LICENSE_PUBLIC_KEY` env (64 hex chars), then a compile-time
baked-in key. Trial users receive the key in the issuance email.

## Per-product behavior

| Product / component | OS/package | License check | Missing license | Placement for trials |
|---|---|---|---|---|
| `razor-bench` (Max/Balanced tiers, threads>1) | Win/Linux CLI | gated tiers refuse; other tiers run Community | clean error naming the exact path tried (exit 1); **no silent fallback for gated tiers** | `--license <path to razor.lic>` + `--license-pubkey <hex>`, or `RAZOR_LICENSE` env, or `razor.lic` next to the exe |
| `razor-watch` service | Win service / Linux systemd | **hard fail without Enterprise** (`LogWatcherService`) | service refuses to start | Linux: `/etc/razor/razor.lic`, mode `0600`, exposed to the unit via `Environment=RAZOR_LICENSE=...` (as provisioned by `install-linux.sh`); Windows: `--license` flag or exe-adjacent file |
| SFX `make` / `stub --stdout` | CLI | `enforce_capability` (`SfxCreator` / `StdoutStreaming`); open extraction stays Community | controlled error, no output | same CLI rule as above |
| `razor-train.exe` (tuning-profile trainer) | Win/Linux | **none — ungated** (no license code; verified by source scan) | n/a — always runs | n/a |
| Benchmark harness (Avalonia GUI/CLI) | Win/Linux | none (open harness, probes engines externally) | n/a | n/a |

## Validation semantics (all products)

`LicenseVerifier::verify`: JSON shape → format match → ed25519 signature →
expiry (`0` = perpetual, else must be future) → denylist → fingerprint
(required **iff** the claims carry one). Trial licenses carry **no**
fingerprint, so they verify on any machine. Outcomes, all runtime-verified:
valid → unlock; expired → `Expired`; edited → `BadSignature`; revoked →
`Revoked`; unreadable → `BadShape` ("cannot read license"); wrong tier for a
capability → tier/capability error. No gate is bypassed for demonstrations.

## File permissions

- Service license (`/etc/razor/razor.lic`): `0600`, root/service-account owned
  (as installed by `install-linux.sh`).
- CLI `razor.lic` next to the exe: readable by the invoking user; restrict to
  your account (the file is a bearer credential for its claims).
- Never commit `.lic` files (public audit forbids them), never log their
  contents (services log statuses only).

## Confidence

- Resolution order + validation semantics: **verified at runtime** (throwaway
  licenses, destroyed after testing).
- Watch service placement: **verified from source** (unit generator +
  `install-linux.sh`) — runtime service test pending watcher re-qualification
  (binary quarantined; unrelated to licensing).
- `razor-train.exe` ungated: **verified from source** (no license references).

## Installer boundary (unchanged)

Installers do **not** create license directories, do not provision license
files, and never overwrite or delete them. License placement is a documented
manual step above (or the issuance email, which repeats the per-product
rule).
