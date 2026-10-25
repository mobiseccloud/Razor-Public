using RazorBenchmark.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
}
