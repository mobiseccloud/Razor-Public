# artifacts/hardened — approved commercial binaries

**Shipped in this release: `razor-train.exe` only** (plus its detached
`.integrity` sidecar). Full details: `MANIFEST.txt` (same directory) and
`approved-binaries.txt` (repo root).

| File | Version/build | Platform | Purpose | SHA-256 | Sidecar | Status |
|---|---|---|---|---|---|---|
| `razor-train.exe` | bundle-20261024-221249 | windows-x64 | tuning-profile trainer | `5FE23DF5…C138B` (full hash in `MANIFEST.txt`) | `razor-train.exe.integrity` (verified match) | VERIFIED: smoke, functional train run, tamper-negative, string scan |

**Verify your download:** recompute SHA-256 and compare with `MANIFEST.txt`
and `approved-binaries.txt`; confirm the `.integrity` sidecar is present.
The loader refuses to run when the sidecar is missing or mismatched
(silent exit 1 — verified). A modified executable does not run.

**Quarantined (NOT shipped):** `razor-bench.exe`, `razor-watch.exe`,
`sfx-decode` tooling — independent functional gates found deterministic
round-trip/verify-decode failures on small inputs in this build
(see `MANIFEST.txt`). They were removed from the allow-list and are not
distributed. The published benchmark evidence (all files >= ~400B, zero
RT-FAILs) is unaffected.

**Nothing else is distributed from this directory.** No source, no PDBs,
no keys, no licenses, no private configuration.
