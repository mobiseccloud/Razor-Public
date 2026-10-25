# Trial license placement — status: PROPOSED PATHS, NOT VERIFIED

## Why this file exists

The release directive proposes these drop-in directories:

- Windows: `%PROGRAMDATA%\MobiSec\Razor\licenses\`
- Linux: `/var/lib/razor/licenses/`

The public repository **cannot verify them**: the license loader is part of the
proprietary commercial engine, whose implementation is not in this repo. No
public test can confirm the loader reads these paths. Claiming otherwise would
be an unverified licensing claim — a hard-stop violation.

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

1. Read the placement instructions in the issuance email (authoritative).
2. Restrict permissions on the file (Windows: Administrators/SYSTEM + your
   account; Linux: `chmod 600`, owned by the service account).
3. Run the engine's license-status action (see the issuance email) to confirm
   acceptance and which tiers unlocked.
4. Without a valid license, Core (harness) features keep working; Pro/Enterprise
   engine tiers report that a trial/paid license is required. A missing license
   is reported as "no license", never as corruption, and no gate is bypassed
   for demonstrations.

## For future verification (private side)

Before any installer claims these paths, the commercial loader's supported path
must be confirmed against its implementation with a signed test license (never
a production key), covering: restrictive permissions, invalid-file rejection,
no-overwrite policy, and no secret leakage in logs. Until that evidence exists
in the release record, this file stays at PROPOSED/UNVERIFIED.
