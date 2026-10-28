# Quickstart (harness)

## Prereqs

- .NET 10 SDK (`dotnet --version` → 10.x).
- A graphical session for the Avalonia GUI, or any shell for the headless commands below.

## Build & run

```bash
dotnet build harness/RazorBenchmark/RazorBenchmark.sln
dotnet run --project harness/RazorBenchmark/RazorBenchmark -- --selftest
```

`--selftest` exercises CSV round-trip, SHA-256 tamper-reject, and trial-email
encoding without launching any client. `--engines` probes PATH availability;
`--request-trial` / `--copy-trial-template` print or open the trial workflow.

## GUI (current scope)

The GUI builds and starts. Four workflows are wired:

1. **Trial tab** — Request Trial by Email / Copy Request Template, with the
   template previewed in-app. Availability of an email client is detected at
   click time; failures fall back to copy/print. (Interactive click-through
   has not been test-executed in this release; URI construction is covered by
   `--selftest`.)
2. **Dashboard CSV view** — `Export CSV` writes the in-memory rows in the
   standard schema (reloadable via `--csvcheck`).
3. **Run Benchmark** (Dashboard) — runs verified cached Suite 1 files
   through every available engine as a cancellable background task, with
   progress/ETA. Unavailable engines are counted as SKIPPED (never
   fabricated). Rows stream into the grid as they complete.
4. **Download Suite 1 Corpus** (Corpora tab) — manifest-pinned download with
   SHA-256 verification (mismatch rejected, retries, local-only skipped).
5. **Compare With Published** (Dashboard) — joins current rows against
   `docs/benchmarks/suite1-max.csv` and reports ratio deviation.

**Headless equivalents** (same engines, no GUI):
`--fetch-manifest <manifestCsv>`, `--run <manifestCsv> --out <outCsv>`.

**Engine paths**: third-party tools resolve via `PATH`
(`--engines` probes). Commercial paths resolve via
`RAZORBENCH_ENGINE_<NAME>_<TIER>` (e.g. `RAZORBENCH_ENGINE_RAZOR_MAX`,
must be an existing file); unset means SKIPPED. No Settings screen exists
yet; nothing is ever auto-downloaded.

**Not implemented in the GUI:** corpus suites beyond Suite 1, engine
enablement/paths UI, and Settings. Do not follow older instructions
describing engine toggles or a Settings screen — those do not exist yet.

## Comparing against published evidence

1. Use the headless `--csvcheck docs/benchmarks/suite1-max.csv` path to
   validate the standard schema, or load a shipped CSV in any spreadsheet.
2. Third-party baselines (`brotli`, `zstd`, `xz`, `gzip`) are detected, never
   downloaded, via `--engines`.
3. A commercial Razor binary is **optional**. No Settings screen exists yet;
   engine paths are configured only where a future release wires them.

## Corpus cache

Default cache locations are shown per manifest entry. Formalslice (private
local-only) entries are marked `local-only` and cannot be fetched by any
current UI action; the downloader service that would use these manifests is
not wired to the GUI yet.
