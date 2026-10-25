# Detached integrity

Where a commercial release ships a detached `.integrity` sidecar next to a
protected binary:

1. The sidecar records the expected digest (SHA-256) plus artifact identity
   (name, version, build date where available).
2. The launcher recomputes the digest of the binary as found on disk.
3. On match → execution proceeds. On mismatch (modified binary) or missing
   sidecar → execution is refused with a clear error and non-zero exit.

> Detached integrity verification detects modification of protected binaries and
> prevents execution when the expected integrity record does not match.

Verification performed per release (when binaries are present under an
allow-list): `scripts/verify-release.ps1` checks accepted / modified-rejected /
missing-rejected. This release ships no binaries, so the check reports SKIP with
reason `no-allow-list` (see `artifacts/hardened/README.md`).

No "tamper-proof" claim is made: integrity detects and refuses; it does not make
modification impossible.
