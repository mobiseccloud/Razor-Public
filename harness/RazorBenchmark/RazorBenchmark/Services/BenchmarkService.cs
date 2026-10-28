using RazorBenchmark.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace RazorBenchmark.Services;

/// <summary>
/// Orchestrates the (file × engine) matrix. Engines run as external processes;
/// round-trip is verified by byte comparison. Times out cleanly per file.
/// </summary>
public sealed class BenchmarkService
{
    public sealed class RunUpdate
    {
        public string Suite { get; set; } = "";
        public string Corpus { get; set; } = "";
        public string File { get; set; } = "";
        public string Engine { get; set; } = "";
        public string Tier { get; set; } = "";
        public double Progress { get; set; }
        public string Eta { get; set; } = "calculating";
        public TimeSpan Elapsed { get; set; }
    }

    private static string BytesHash(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs));
    }

    public static double MbPerSec(long bytes, long ms)
        => ms <= 0 ? 0 : Math.Round(bytes / (ms / 1000.0) / 1e6, 3);

    public static string EtaString(TimeSpan elapsed, double done, double total)
    {
        if (done <= 0 || total <= 0 || done >= total) return "calculating";
        var per = elapsed.TotalSeconds / done;
        var left = per * (total - done);
        if (double.IsNaN(left) || double.IsInfinity(left) || left < 0) return "calculating";
        return TimeSpan.FromSeconds(left).ToString(@"mm\:ss");
    }

    /// <summary>
    /// Reference check used by the harness self-test: byte-identical copy round-trip.
    /// Real engine cells shell out via <see cref="EngineRunner"/> and reuse this comparison.
    /// </summary>
    public static bool RoundTripOk(string original, string roundTripped)
    {
        try
        {
            return BytesHash(original) == BytesHash(roundTripped);
        }
        catch { return false; }
    }

    public static BenchmarkRow ToRow(string suite, string corpus, string file,
        string engine, string tier, long orig, long comp,
        long encMs, long decMs, bool ok, string status,
        string host, int threads, string binVer)
        => new(suite, corpus, file, engine, tier, orig, comp,
            Math.Round(100.0 * comp / Math.Max(1, orig), 2),
            encMs, decMs, MbPerSec(orig, encMs), MbPerSec(orig, decMs),
            ok ? "OK" : "MISMATCH", status,
            DateTime.UtcNow.ToString("o"), host, threads, binVer, "archival");

    /// <summary>
    /// Resolves an engine executable: explicit per-engine env override
    /// (RAZORBENCH_ENGINE_&lt;NAME&gt;_&lt;TIER&gt;, must be an existing file),
    /// else the catalog path looked up on PATH. Returns null when
    /// unconfigured (caller records SKIPPED, never fabricates a run).
    /// </summary>
    public static string? ResolveExecutable(EngineSpec spec)
    {
        var key = "RAZORBENCH_ENGINE_"
            + new string(spec.Name.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_').ToArray())
            + "_" + new string(spec.Tier.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_').ToArray());
        var overridePath = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;
        if (!string.IsNullOrWhiteSpace(spec.Executable)
            && EngineRunner.ExistsOnPath(spec.Executable))
            return spec.Executable;
        return null;
    }

    public sealed class MatrixTotals
    {
        public int FilesAttempted;
        public int EnginesAttempted;
        public int Passed;
        public int RoundTripFailures;
        public int Errors;
        public int Timeouts;
        public int Skipped;
    }

    /// <summary>
    /// Runs the (file × engine) matrix. Engines execute via shell so arg
    /// templates with redirection work identically on Windows and Unix.
    /// Emits rows ONLY for measured cells (PASS with sizes, RT-FAIL with
    /// measured sizes + MISMATCH). ERROR/TIMEOUT/SKIPPED cells are counted in
    /// <paramref name="totals"/> and reported by the caller — never as
    /// pseudo-data rows (keeps every emitted CSV inside the verified schema:
    /// positive sizes, consistent ratio, no unverified claims).
    /// </summary>
    public static async Task<List<BenchmarkRow>> RunMatrixAsync(
        string suite, string corpus, IReadOnlyList<string> files,
        IReadOnlyList<EngineSpec> engines,
        Func<EngineSpec, string?> resolveExe,
        string host, int threads, string binVer,
        IProgress<RunUpdate>? progress, CancellationToken ct,
        int timeoutMsPerCell = 600_000)
    {
        var rows = new List<BenchmarkRow>();
        var totals = new MatrixTotals
        {
            FilesAttempted = files.Count,
            EnginesAttempted = engines.Count
        };
        var done = 0;
        var total = (long)files.Count * engines.Count;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            long orig;
            try { orig = new FileInfo(file).Length; }
            catch { totals.Errors++; continue; }
            if (orig <= 0) { totals.Errors++; continue; }
            foreach (var engine in engines)
            {
                ct.ThrowIfCancellationRequested();
                var exe = resolveExe(engine);
                if (string.IsNullOrWhiteSpace(exe))
                {
                    totals.Skipped++;
                    done++;
                    continue;
                }
                var work = Path.Combine(Path.GetTempPath(),
                    "razorbench-cell-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(work);
                try
                {
                    var encOut = Path.Combine(work, "out.enc");
                    var decOut = Path.Combine(work, "out.dec");
                    // Quote substituted paths: engine templates run through
                    // the platform shell, so spaces must survive intact.
                    var encCmd = engine.CompressArgs
                        .Replace("{input}", "\"" + file + "\"")
                        .Replace("{output}", "\"" + encOut + "\"");
                    var enc = await EngineRunner.RunShellAsync(
                        exe, encCmd, work, timeoutMsPerCell, ct);
                    if (enc.TimedOut) { totals.Timeouts++; done++; continue; }
                    if (enc.ExitCode != 0 || !File.Exists(encOut))
                    {
                        totals.Errors++;
                        done++;
                        continue;
                    }
                    var comp = new FileInfo(encOut).Length;
                    if (comp <= 0) { totals.Errors++; done++; continue; }
                    var decCmd = engine.DecompressArgs
                        .Replace("{input}", "\"" + encOut + "\"")
                        .Replace("{output}", "\"" + decOut + "\"");
                    var dec = await EngineRunner.RunShellAsync(
                        exe, decCmd, work, timeoutMsPerCell, ct);
                    if (dec.TimedOut) { totals.Timeouts++; done++; continue; }
                    if (dec.ExitCode != 0 || !File.Exists(decOut))
                    {
                        totals.Errors++;
                        done++;
                        continue;
                    }
                    var ok = RoundTripOk(file, decOut);
                    rows.Add(ToRow(suite, corpus,
                        Path.GetFileName(file), engine.Name, engine.Tier,
                        orig, comp, enc.ElapsedMs, dec.ElapsedMs, ok,
                        ok ? "PASS" : "RT-FAIL",
                        host, threads, binVer));
                    if (ok) totals.Passed++; else totals.RoundTripFailures++;
                    done++;
                    progress?.Report(new RunUpdate
                    {
                        Suite = suite,
                        Corpus = corpus,
                        File = Path.GetFileName(file),
                        Engine = engine.Name + "/" + engine.Tier,
                        Progress = 100.0 * done / Math.Max(1, total),
                        Eta = EtaString(sw.Elapsed, done, total),
                        Elapsed = sw.Elapsed,
                    });
                }
                finally
                {
                    try { Directory.Delete(work, true); } catch { }
                }
            }
        }
        LastTotals = totals;
        return rows;
    }

    /// <summary>Totals of the most recent <see cref="RunMatrixAsync"/> call.</summary>
    public static MatrixTotals LastTotals { get; private set; } = new();

    /// <summary>
    /// Compares measured rows against published evidence rows on
    /// (file, engine, tier). Returns a human summary; never mutates inputs.
    /// </summary>
    public static string CompareWithPublished(
        IReadOnlyList<BenchmarkRow> measured,
        IReadOnlyList<BenchmarkRow> published)
    {
        var pub_ = published
            .GroupBy(r => (r.File, r.Engine, r.Tier))
            .ToDictionary(g => g.Key, g => g.First());
        var compared = 0;
        var devSum = 0.0;
        var worst = "";
        var worstDev = 0.0;
        var missing = 0;
        foreach (var m in measured)
        {
            if (!pub_.TryGetValue((m.File, m.Engine, m.Tier), out var p))
            {
                missing++;
                continue;
            }
            compared++;
            var dev = Math.Abs(m.RatioPercent - p.RatioPercent);
            devSum += dev;
            if (dev > worstDev) { worstDev = dev; worst = m.File + " " + m.Engine + "/" + m.Tier; }
        }
        if (compared == 0)
            return $"Compared 0 rows ({missing} measured rows have no published counterpart).";
        return $"Compared {compared} rows ({missing} without published counterpart): "
            + $"mean abs ratio deviation {Math.Round(devSum / compared, 3)}pp, "
            + $"worst {worst} at {Math.Round(worstDev, 3)}pp.";
    }
}
