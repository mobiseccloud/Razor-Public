# ARCHITECTURE

## Boundary

```
OPEN TEST HARNESS (MIT — this repo)
  harness/      Avalonia GUI + CSV pipeline + corpus manifests + engine adapters
  docs/         methodology, results, corpus, format-subset, security, trial docs
  scripts/      audit + verification (no secrets)
  .github/      CI (build harness, audit, CSV schema check — no secrets)
        │  process boundary: engines are spawned as child processes,
        │  results return via files + exit codes + stdout parsing
        ▼
COMMERCIAL RAZOR ENGINE (proprietary — NOT in this repo)
  archival / streaming tiers, protection features, licensing
        │
        ▼
OPTIONAL PROTECTION / ARCHIVAL SERVICES (commercial)
  detached .integrity sidecars, SFX, password / online-gated extraction
```

Rules:

- The harness **never references or compiles private Razor source**. Engine support
  means: "run this executable with these argv, parse its output, verify round-trip".
- A published binary never implies its source may be published.
- Trial/licensing flows are documented as boundaries (request format + service
  responsibilities); signing keys, issuer config, databases, issued licenses,
  SMTP/API tokens, and infrastructure config are never in this repo.

## Harness internals (open)

- `Models/` — `BenchmarkRow` (exact public CSV schema), `CorpusEntry` (manifest
  entry), `EngineSpec` (exe, argv templates, availability).
- `Services/` — `CsvService` (read/write/validate schema), `ManifestService`
  (parse manifests, compute SHA-256), `DownloaderService` (progress, retry,
  hash-verify), `EngineRunner` (process spawn, timeout, capture), `EngineCatalog`
  (PATH probing, missing-tool guidance), `BenchmarkService` (matrix orchestration,
  round-trip check, ETA).
- `ViewModels/` + `Views/` — Dashboard (CSV view/export), Trial tab
  (request/copy), corpus list, engine availability report. Run, download,
  engine toggles, and Settings are not wired yet.
- No network fetch of engines. Missing third-party tools are reported by
  `--engines` with official install documentation; any install flow is
  explicit and user-approved.
- `BenchmarkService` (matrix orchestration), `DownloaderService` (progress,
  retry, hash-verify), `ManifestService`, `EngineRunner`, and `EngineCatalog`
  exist as compiled-but-UI-unwired library code: no control invokes them, and
  the Dashboard progress/ETA/byte fields have no producer. They are covered
  only by headless `--selftest`/`--csvcheck` paths where applicable.

## Commercial side (opaque to this repo)

Tiers (reference names used by the harness UI): Max, MaxFast, Fast/Stream,
Balanced (where licensed). Threading/license gating is enforced by the commercial
binary itself; the harness only passes flags and records what happened.
