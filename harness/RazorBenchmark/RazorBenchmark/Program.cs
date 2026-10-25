using Avalonia;
using RazorBenchmark.Models;
using RazorBenchmark.Services;
using System;

namespace RazorBenchmark;

internal sealed class Program
{
    /// <summary>
    /// Headless self-checks (CI / smoke): --csvcheck &lt;path&gt; reloads a
    /// standard-schema CSV; --engines lists engine availability. Otherwise
    /// starts the GUI.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--csvcheck")
        {
            try
            {
                var rows = CsvService.Read(args[1]);
                Console.WriteLine($"CSVCHECK OK: {rows.Count} rows from {args[1]}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("CSVCHECK FAIL: " + ex.Message);
                return 1;
            }
        }
        if (args.Length == 1 && args[0] == "--selftest")
        {
            try
            {
                // 1. CSV write -> read round-trip (standard schema).
                var rows = new[]
                {
                    new Models.BenchmarkRow("suite1", "calgary", "bib", "brotli", "q11",
                        111261, 28365, 25.49, 158, 0, 0.704, 0, "OK", "PASS",
                        "2026-10-24T00:00:00Z", "selftest", 1, "selftest", "archival"),
                };
                var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "razorbench-selftest-" + Guid.NewGuid().ToString("N") + ".csv");
                Services.CsvService.Write(tmp, rows);
                var back = Services.CsvService.Read(tmp);
                System.IO.File.Delete(tmp);
                if (back.Count != 1 || back[0].CompressedBytes != 28365 || back[0].Status != "PASS")
                    throw new InvalidOperationException("CSV round-trip mismatch.");
                // 2. SHA-256 accept + tamper (single-bit flip) rejection.
                var f = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "razorbench-selftest-" + Guid.NewGuid().ToString("N") + ".bin");
                System.IO.File.WriteAllBytes(f, new byte[] { 1, 2, 3, 4 });
                var good = Services.ManifestService.Sha256Of(f);
                System.IO.File.WriteAllBytes(f, new byte[] { 1, 2, 3, 5 });
                var bad = Services.ManifestService.Sha256Of(f);
                System.IO.File.Delete(f);
                if (good == bad) throw new InvalidOperationException("SHA-256 did not detect tamper.");
                Console.WriteLine("SELFTEST OK: csv-roundtrip + sha256 tamper-reject");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELFTEST FAIL: " + ex.Message);
                return 1;
            }
        }
        {
            foreach (var e in EngineCatalog.Defaults())
            {
                var avail = string.IsNullOrWhiteSpace(e.Executable)
                    ? "not-configured"
                    : EngineRunner.ExistsOnPath(e.Executable) ? "available" : "unavailable";
                Console.WriteLine($"{e.Name}/{e.Tier}: {avail} (help: {e.InstallHelpUrl})");
            }
            return 0;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
