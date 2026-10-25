# SECURITY

## Scope

This file covers the **public release surface**: the open harness, documentation,
benchmark data, and (when shipped under an allow-list) the handling of commercial
artifacts. The proprietary engine implementation is out of scope for public review
by design.

## Reporting

- Report harness/security-doc issues via this repository's issue tracker.
- For sensitive reports (e.g., suspected credential exposure in public material):
  email `razor@mobisec.cloud` with subject `[SECURITY] <summary>`, include the
  file path / commit SHA, and do not open a public issue first.
- We will acknowledge receipt, assess, and publish a fix + note in CHANGELOG.

## Tested protection mechanisms (where shipped in commercial artifacts)

- **Detached integrity:** a `.integrity` sidecar records the expected digest of a
  protected binary. The launcher verifies the record before execution:
  > Detached integrity verification detects modification of protected binaries and
  > prevents execution when the expected integrity record does not match.
- **Tamper-negative behaviour:** modified binary → execution refused; missing
  sidecar → execution refused. (Verified per release by `scripts/verify-release.ps1`
  when binaries are present under an allow-list. This release ships no binaries —
  see `artifacts/hardened/README.md`.)
- **SFX password (offline mode, where shipped):** AES-256-GCM with Argon2id-derived
  key. Incorrect password or corrupted payload fails at the authentication tag —
  no plaintext is produced.
- **Online-gated extraction (where shipped):** policy-controlled token-gated
  extraction with optional online authorization. Where the implementation validates
  token structure/claims locally and then contacts an authorization endpoint, it is
  described exactly that way — **not** as local cryptographic JWT signature
  verification.

## Non-claims

- No "tamper-proof", "unbreakable", "military-grade", or "zero-corruption guarantee"
  claim is made.
- No "independently security audited" claim is made.
- Integrity and SFX features raise the cost of casual tampering and detect
  modification; they are not proofs against a resourced adversary.

## Hygiene in this repo

- No signing keys, production licenses, credentials, tokens, SMTP/API secrets,
  `.env` contents, or infrastructure configuration are committed — enforced by
  `scripts/audit-public-release.ps1|.sh` in CI.
- Workflows contain no secrets.
