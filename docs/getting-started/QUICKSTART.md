# Quickstart (harness)

## Prereqs

- .NET 10 SDK (`dotnet --version` → 10.x).
- Third-party engines you want to compare (any subset): `brotli`, `zstd`, `xz`,
  `gzip` on PATH (harness detects and reports availability).
- A Razor commercial binary is **optional**. If present, point the harness at it
  in Settings → Engine paths. The harness never downloads executables silently.

## Build & run

```bash
dotnet build harness/RazorBenchmark/RazorBenchmark.sln
dotnet run --project harness/RazorBenchmark/RazorBenchmark
```

## First benchmark

1. Open the **Corpora** tab → pick **Suite 1 Core**.
2. Click **Verify / Download** — the downloader fetches from manifest URLs and
   rejects hash mismatches.
3. Open **Engines** → enable the installed engines.
4. Open **Dashboard** → **Run** → watch progress/ETA ("calculating" until stable).
5. **Export CSV** (standard schema) → **Reload** to review inside the app.
6. Diff against `docs/benchmarks/suite1-max.csv` on sizes/ratios/status.

## Corpus cache

Default cache locations are shown per manifest entry and configurable in Settings.
Formalslice (private local-only) entries are marked `local-only` and skipped by
the downloader — they are not required.
