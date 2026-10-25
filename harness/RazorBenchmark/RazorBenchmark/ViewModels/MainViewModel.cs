using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RazorBenchmark.Models;
using RazorBenchmark.Services;
using System;
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
    [ObservableProperty] private string _processStatus = "Idle. Pick a suite tab, verify the corpus, select engines, then Run.";
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
}
