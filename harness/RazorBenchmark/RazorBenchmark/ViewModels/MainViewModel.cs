using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RazorBenchmark.Models;
using RazorBenchmark.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RazorBenchmark.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private string _suite = "suite1";
    [ObservableProperty] private string _currentFile = "(idle)";
    [ObservableProperty] private string _engine = "(none)";
    [ObservableProperty] private string _tier = "(none)";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _elapsed = "00:00";
    [ObservableProperty] private string _eta = "calculating";
    [ObservableProperty] private string _originalBytes = "-";
    [ObservableProperty] private string _compressedBytes = "-";
    [ObservableProperty] private string _ratio = "-";
    [ObservableProperty] private string _roundTrip = "-";
    [ObservableProperty] private string _processStatus = "Idle. Trial tab, Dashboard Export CSV, benchmark Run, and corpus Download are wired. Engine setup UI is not implemented yet — commercial paths resolve via RAZORBENCH_ENGINE_<NAME>_<TIER>, third-party tools via PATH.";
    [ObservableProperty] private string _engineAvailability = "Probing engines on PATH…";
    [ObservableProperty] private string _trialStatus =
        "No email has been sent. Requesting a trial opens YOUR email app with a prefilled message — you review and send it yourself. Available without a Pro/Enterprise license.";
    [ObservableProperty] private string _trialTemplatePreview = TrialEmail.TemplateBody;

    public ObservableCollection<BenchmarkRow> Rows { get; } = new();
    public ObservableCollection<string> Suites { get; } = new(new[]
        { "Suite 1 Core", "Neuro", "Genomic", "Logs", "Adversarial", "Enwik9 Scale" });
    public ObservableCollection<EngineSpec> Engines { get; } = new(EngineCatalog.Defaults());

    private CancellationTokenSource _cts = new();

    public MainViewModel()
    {
        RefreshEngineAvailability();
    }

    private void RefreshEngineAvailability()
    {
        var parts = Engines.Select(e =>
            string.IsNullOrWhiteSpace(e.Executable)
                ? $"{e.Name}/{e.Tier}: path not configured (see Settings; nothing is auto-downloaded)"
                : EngineRunner.ExistsOnPath(e.Executable)
                    ? $"{e.Name}/{e.Tier}: available"
                    : $"{e.Name}/{e.Tier}: unavailable — see {e.InstallHelpUrl}");
        EngineAvailability = string.Join(Environment.NewLine, parts);
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        ProcessStatus = "Exporting CSV (standard schema)…";
        await Task.Run(() =>
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"razorbench-{Suite}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
            CsvService.Write(path, Rows.ToList());
        });
        ProcessStatus = $"Exported {Rows.Count} rows.";
    }

    [RelayCommand]
    private void RequestTrial()
    {
        var uri = TrialEmail.BuildMailtoUri();
        if (!TrialEmail.IsWithinSafeLength(uri))
        {
            TrialStatus = "The prefilled message is too long for an email link on this system. "
                + "Use Copy Request Template and paste it into an email to "
                + TrialEmail.Recipient + " yourself. Nothing has been sent.";
            return;
        }
        var (outcome, message) = TrialEmail.TryOpenEmailClient(uri);
        TrialStatus = outcome == TrialEmail.LaunchOutcome.Opened
            ? message + " The application cannot tell whether you pressed Send."
            : message + " Nothing has been sent.";
    }

    [RelayCommand]
    private async Task CopyTrialTemplateAsync()
    {
        try
        {
            var clipboard = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;
            if (clipboard is null)
            {
                TrialStatus = "Clipboard is unavailable here. Select the template text below and copy it manually, "
                    + "then email it to " + TrialEmail.Recipient + ". Nothing has been sent.";
                return;
            }
            await clipboard.SetTextAsync(
                $"To: {TrialEmail.Recipient}\nSubject: {TrialEmail.Subject}\n\n{TrialEmail.TemplateBody}");
            TrialStatus = "Request template copied. Paste it into an email to "
                + TrialEmail.Recipient + " and send it yourself. Nothing has been sent automatically.";
        }
        catch (Exception ex)
        {
            TrialStatus = "Copy failed (" + ex.GetType().Name + "). Select the template text below and copy it manually. Nothing has been sent.";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        ProcessStatus = "Cancelled by user.";
    }

    private static string? FindInAncestors(string relative)
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        for (var i = 0; i < 12 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, relative);
            if (System.IO.File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static string CorpusCacheRoot()
        => ManifestService.CorpusCacheRoot();

    /// <summary>
    /// Resolves a manifest cache_location template (%CORPUS_CACHE% prefix,
    /// either slash style) to a local path. Never fabricates directories.
    /// </summary>
    public static string ResolveCachePath(string template)
        => ManifestService.ResolveCachePath(template);

    [RelayCommand]
    private async Task DownloadCorpusAsync()
    {
        // V1 scope: the Suite 1 manifest is the only fully-pinned public
        // suite (Suite 2 entries are predominantly local-only). Other suite
        // selections report this instead of pretending.
        if (!Suite.StartsWith("Suite 1", StringComparison.OrdinalIgnoreCase))
        {
            ProcessStatus = $"Suite '{Suite}': only the Suite 1 manifest is wired for download yet.";
            return;
        }
        var manifestPath = FindInAncestors(
            System.IO.Path.Combine("docs", "benchmarks", "manifest-suite1.csv"));
        if (manifestPath is null)
        {
            ProcessStatus = "manifest-suite1.csv not found (repo checkout required for corpus download).";
            return;
        }
        var dirOf = new Func<CorpusEntry, string>(entry =>
        {
            var dest = ResolveCachePath(entry.CacheLocation);
            return System.IO.Path.GetDirectoryName(dest)!;
        });
        var progress = new Progress<(CorpusEntry Entry, DownloadProgress P)>(t =>
            ProcessStatus = $"Downloading {t.Entry.File}: {t.P.BytesReceived}/{t.P.TotalBytes ?? t.Entry.ExpectedBytes} [{t.P.State}]");
        DownloaderService.FetchSummary summary;
        try
        {
            summary = await DownloaderService.FetchManifestAsync(
                manifestPath, dirOf, progress, 3, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            ProcessStatus = "Cancelled by user.";
            return;
        }
        ProcessStatus = $"Download complete: {summary.Verified} verified, "
            + $"{summary.LocalOnlySkipped} local-only skipped"
            + (summary.Failed.Count == 0 ? "."
                : $", {summary.Failed.Count} FAILED: {string.Join("; ", summary.Failed.Take(5))}");
    }

    [RelayCommand]
    private async Task RunBenchmarkAsync()
    {
        // V1 scope: runs verified-present Suite 1 cache files through every
        // resolvable engine. Unavailable engines become SKIPPED counts (never
        // pseudo-rows). Existing Rows are kept; new rows stream in.
        var manifestPath = FindInAncestors(
            System.IO.Path.Combine("docs", "benchmarks", "manifest-suite1.csv"));
        if (manifestPath is null)
        {
            ProcessStatus = "manifest-suite1.csv not found (repo checkout required to enumerate the suite).";
            return;
        }
        List<CorpusEntry> entries;
        try { entries = ManifestService.Read(manifestPath).ToList(); }
        catch (Exception ex)
        {
            ProcessStatus = "Manifest parse failed: " + ex.Message;
            return;
        }
        var files = new List<(string Corpus, string Path)>();
        var missing = 0;
        foreach (var entry in entries)
        {
            var local = ResolveCachePath(entry.CacheLocation);
            if (System.IO.File.Exists(local)
                && ManifestService.Verify(entry, local, out _))
                files.Add((entry.Corpus, local));
            else
                missing++;
        }
        if (files.Count == 0)
        {
            ProcessStatus = $"No verified corpus files cached ({missing} missing). Use Download first.";
            return;
        }
        var groups = files.GroupBy(f => f.Corpus).ToList();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var progress = new Progress<BenchmarkService.RunUpdate>(u =>
        {
            CurrentFile = u.File;
            Engine = u.Engine;
            Progress = u.Progress;
            Eta = u.Eta;
            Elapsed = u.Elapsed.ToString(@"mm\:ss");
            ProcessStatus = $"Running {u.File} [{u.Engine}] ({u.Progress:F1}%)";
        });
        try
        {
            foreach (var group in groups)
            {
                var rows = await BenchmarkService.RunMatrixAsync(
                    "suite1", group.Key,
                    group.Select(f => f.Path).ToList(),
                    Engines.ToList(),
                    BenchmarkService.ResolveExecutable,
                    Environment.MachineName, 1, "harness-gui",
                    progress, _cts.Token);
                var t = BenchmarkService.LastTotals;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    foreach (var row in rows) Rows.Add(row);
                });
                ProcessStatus = $"Corpus {group.Key}: {rows.Count} rows "
                    + $"(pass {t.Passed}, rt-fail {t.RoundTripFailures}, "
                    + $"error {t.Errors}, timeout {t.Timeouts}, skipped {t.Skipped}).";
            }
            sw.Stop();
            Elapsed = sw.Elapsed.ToString(@"mm\:ss");
            Progress = 100;
        }
        catch (OperationCanceledException)
        {
            ProcessStatus = "Cancelled by user.";
        }
        catch (Exception ex)
        {
            ProcessStatus = "Run failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void CompareWithPublished()
    {
        var publishedPath = FindInAncestors(
            System.IO.Path.Combine("docs", "benchmarks", "suite1-max.csv"));
        if (publishedPath is null)
        {
            ProcessStatus = "suite1-max.csv not found (repo checkout required for comparison).";
            return;
        }
        try
        {
            var published = CsvService.Read(publishedPath);
            ProcessStatus = BenchmarkService.CompareWithPublished(
                Rows.ToList(), published);
        }
        catch (Exception ex)
        {
            ProcessStatus = "Compare failed: " + ex.Message;
        }
    }
}
