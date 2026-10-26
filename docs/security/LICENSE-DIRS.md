# Trial license placement — status: VERIFIED (see LICENSE-LOADER-PATHS.md)

## Authoritative rule

The engine resolves its license in this order (verified against the loader
source and runtime-tested with throwaway licenses):

1. `--license <path>` flag (highest precedence).
2. `RAZOR_LICENSE` environment variable (a file path).
3. `razor.lic` next to the current executable.
4. Fallback: relative `razor.lic` (working directory).

The vendor public key comes from `--license-pubkey <hex>`,
`RAZOR_LICENSE_PUBLIC_KEY` env, or a baked-in build key. Trial licenses
carry no device fingerprint and verify on any machine.

Full per-product table (watch service placement, permissions, validation
semantics, confidence levels): [`LICENSE-LOADER-PATHS.md`](LICENSE-LOADER-PATHS.md).

The `%PROGRAMDATA%\MobiSec\Razor\licenses\` and `/var/lib/razor/licenses/`
drop-in directories previously proposed here are **retired**: the loader
reads none of them. The one exception is the watch service's own
`/etc/razor/razor.lic` (mode `0600`), which is exposed to the unit through
`Environment=RAZOR_LICENSE=...`, i.e. rule 2 above — not a scanned
directory.

## Policy enforced by this release

1. **Installers do NOT create license directories and do NOT claim a drop-in
   path works.** No package in `packaging/` provisions the paths above.
2. **The authoritative placement is defined by the license-issuance email.**
   When the private licensing service returns a trial license, its accompanying
   instructions state exactly where that build reads it from. Follow those.
3. **The harness/installers never generate licenses locally**, never embed
   signing keys or license-generation source, and never trust an arbitrary
   `.lic` file merely because it is present somewhere.
4. License acceptance is reported only by the commercial binary's own
   signature/entitlement validation (malformed, expired, modified, or invalid
   files are rejected by that loader; its exact messages are the engine's own).
5. Installers never overwrite an existing valid license: they do not manage
   license files at all.
6. Logs never print secrets or full license contents (public code prints only
   statuses and non-sensitive paths).

## If you received a trial license

1. Place it per the resolution rule above (recommended: `--license` flag or
   `razor.lic` next to the product binary; services use `RAZOR_LICENSE`).
   The issuance email repeats the per-product rule.
2. Restrict permissions on the file (Windows: Administrators/SYSTEM + your
   account; Linux: `chmod 600`, owned by the service account).
3. Run the product with a gated action (e.g. `razor-bench --arch-tier max`
   with `--license-pubkey <hex>` from the issuance email) to confirm
   acceptance and which tiers unlocked.
4. Without a valid license, Core (harness) features keep working; Pro/Enterprise
   engine tiers report that a trial/paid license is required. A missing license
   is reported as "no license", never as corruption, and no gate is bypassed
   for demonstrations.

## Verification record (private side, 2026-10-26)

Loader paths were confirmed against the implementation with throwaway
licenses (never production keys): resolution precedence matrix, valid /
expired / tampered / missing behaviors, and a full trial-unlock run
(`--license` + `--license-pubkey` → Max tier, round-trip clean). Throwaway
material destroyed after testing. Watch-service runtime test remains pending
watcher re-qualification (binary quarantined; unrelated to licensing).
