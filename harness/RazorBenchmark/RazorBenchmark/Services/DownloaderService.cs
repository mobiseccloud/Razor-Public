using RazorBenchmark.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RazorBenchmark.Services;

public sealed class DownloadProgress
{
    public long BytesReceived { get; set; }
    public long? TotalBytes { get; set; }
    public string State { get; set; } = "downloading";
}

public static class DownloaderService
{
    private static readonly HttpClient Http = new();

    /// <summary>
    /// Downloads a manifest entry with retry + SHA-256 verification.
    /// Local-only entries are skipped (not an error). Hash mismatch => file
    /// deleted and exception — never silently accepted.
    /// </summary>
    public static async Task<string> FetchAsync(
        CorpusEntry entry, string cacheDir,
        IProgress<DownloadProgress>? progress,
        int maxRetries, CancellationToken ct)
    {
        if (entry.SourceUrl == "local-only" || entry.SourceUrl == "see-CORPUS-md")
        {
            var local = Path.Combine(cacheDir, entry.File);
            if (File.Exists(local) && ManifestService.Verify(entry, local, out _))
                return local;
            throw new InvalidOperationException(
                $"Dataset '{entry.File}' is not publicly downloadable from here (see CORPUS.md).");
        }

        Directory.CreateDirectory(cacheDir);
        var dest = Path.Combine(cacheDir, entry.File);
        if (File.Exists(dest) && ManifestService.Verify(entry, dest, out _))
        {
            progress?.Report(new DownloadProgress { BytesReceived = new FileInfo(dest).Length, TotalBytes = entry.ExpectedBytes, State = "verified" });
            return dest;
        }

        Exception? last = null;
        for (var attempt = 1; attempt <= Math.Max(1, maxRetries); attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var resp = await Http.GetAsync(entry.SourceUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength;
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var fs = new FileStream(dest + ".part", FileMode.Create, FileAccess.Write, FileShare.None);
                var buf = new byte[1 << 16];
                long got = 0;
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    await fs.WriteAsync(buf.AsMemory(0, n), ct);
                    got += n;
                    progress?.Report(new DownloadProgress { BytesReceived = got, TotalBytes = total ?? entry.ExpectedBytes });
                }
                fs.Close();
                if (!ManifestService.Verify(entry, dest + ".part", out var actual))
                {
                    File.Delete(dest + ".part");
                    throw new InvalidDataException($"SHA-256 mismatch for {entry.File}: expected {entry.Sha256}, got {actual}. Rejected.");
                }
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(dest + ".part", dest);
                progress?.Report(new DownloadProgress { BytesReceived = entry.ExpectedBytes, TotalBytes = entry.ExpectedBytes, State = "verified" });
                return dest;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                last = ex;
                progress?.Report(new DownloadProgress { State = $"retry {attempt}/{maxRetries}: {ex.Message}" });
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
        }
        throw last ?? new IOException("Download failed for " + entry.File);
    }

    public sealed class FetchSummary
    {
        public int Verified;
        public int LocalOnlySkipped;
        public List<string> Failed { get; } = new();
    }

    /// <summary>
    /// Downloads every entry of a manifest file (shared by the GUI Download
    /// button and headless --fetch-manifest). Local-only entries are skipped,
    /// failures are collected (never thrown past the summary), cancellation
    /// is honored. Returns counts; callers render them.
    /// </summary>
    public static async Task<FetchSummary> FetchManifestAsync(
        string manifestPath,
        Func<CorpusEntry, string> resolveCacheDir,
        IProgress<(CorpusEntry Entry, DownloadProgress Progress)>? progress,
        int maxRetries,
        CancellationToken ct)
    {
        var summary = new FetchSummary();
        List<CorpusEntry> entries;
        try { entries = ManifestService.Read(manifestPath).ToList(); }
        catch (Exception ex)
        {
            summary.Failed.Add("manifest: " + ex.Message);
            return summary;
        }
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var dir = resolveCacheDir(entry);
                var localProgress = new Progress<DownloadProgress>(p =>
                    progress?.Report((entry, p)));
                await FetchAsync(entry, dir, localProgress, maxRetries, ct);
                summary.Verified++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not publicly downloadable"))
            {
                summary.LocalOnlySkipped++;
            }
            catch (Exception ex)
            {
                summary.Failed.Add(entry.File + ": " + ex.Message);
            }
        }
        return summary;
    }
}
