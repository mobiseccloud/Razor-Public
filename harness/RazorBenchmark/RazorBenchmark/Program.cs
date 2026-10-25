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
                // 3. Trial-email URI construction + encoding (no client launched).
                if (TrialEmail.Recipient != "razor@mobisec.cloud")
                    throw new InvalidOperationException("trial recipient mismatch");
                if (TrialEmail.Subject != "RazorZip 14-Day Enterprise Trial Request")
                    throw new InvalidOperationException("trial subject mismatch");
                foreach (var field in new[] { "Email Address:", "Given Name:", "Last Name:",
                    "Cell Number:", "Business Name:", "Heard About From:", "Country:",
                    "Town/City:", "--- BEGIN TRIAL REQUEST ---", "--- END TRIAL REQUEST ---" })
                    if (!TrialEmail.TemplateBody.Contains(field))
                        throw new InvalidOperationException("trial template field missing: " + field);
                var mailto = TrialEmail.BuildMailtoUri();
                if (!mailto.StartsWith("mailto:razor%40mobisec.cloud?subject=", StringComparison.Ordinal))
                    throw new InvalidOperationException("mailto recipient/subject encoding wrong");
                // Round-trip: decode the body and require byte-exact template preservation.
                var bodyEnc = mailto.Substring(mailto.IndexOf("&body=", StringComparison.Ordinal) + "&body=".Length);
                if (Uri.UnescapeDataString(bodyEnc) != TrialEmail.TemplateBody)
                    throw new InvalidOperationException("mailto body does not preserve template exactly");
                // Encoding spot-checks: CRLF, brackets, unicode, punctuation.
                if (!mailto.Contains("%0D%0A") && !mailto.Contains("%0A"))
                    throw new InvalidOperationException("line-break encoding missing");
                if (!mailto.Contains("%5B") || !mailto.Contains("%5D"))
                    throw new InvalidOperationException("bracket encoding missing");
                var uni = TrialEmail.BuildMailtoUri(TrialEmail.TemplateBody + "Grüße — test ✓");
                if (!uni.Contains("%C3%BC") || !uni.Contains("%E2%9C%93"))
                    throw new InvalidOperationException("unicode encoding missing");
                // Safe-length + embedded-template consistency with the source of truth.
                if (!TrialEmail.IsWithinSafeLength(mailto))
                    throw new InvalidOperationException("default template exceeds safe mailto length");
                var truth = FindInAncestors("docs/trial/trial-request-template.txt");
                if (truth is not null)
                {
                    var onDisk = System.IO.File.ReadAllText(truth).Replace("\r\n", "\n");
                    if (onDisk != TrialEmail.TemplateBody.Replace("\r\n", "\n"))
                        throw new InvalidOperationException("embedded template differs from docs/trial/trial-request-template.txt");
                }
                Console.WriteLine("SELFTEST OK: csv-roundtrip + sha256 tamper-reject + trial-email encoding");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELFTEST FAIL: " + ex.Message);
                return 1;
            }
        }
        if (args.Length == 1 && args[0] == "--engines")
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
        if (args.Length == 1 && (args[0] == "--request-trial" || args[0] == "--copy-trial-template"))
        {
            // CLI helper: graphical sessions get the OS email handler; headless
            // sessions get the printable fallback. Never sends anything itself.
            if (args[0] == "--copy-trial-template")
            {
                Console.WriteLine(TrialEmail.ManualInstructions());
                return 0;
            }
            if (TrialEmail.IsHeadless())
            {
                Console.WriteLine(TrialEmail.ManualInstructions());
                return 0;
            }
            var uri = TrialEmail.BuildMailtoUri();
            if (!TrialEmail.IsWithinSafeLength(uri))
            {
                Console.WriteLine(TrialEmail.ManualInstructions());
                return 0;
            }
            var (outcome, message) = TrialEmail.TryOpenEmailClient(uri);
            Console.WriteLine(message);
            if (outcome != TrialEmail.LaunchOutcome.Opened)
            {
                Console.WriteLine();
                Console.WriteLine(TrialEmail.ManualInstructions());
                return 2; // handler missing/failed: caller must use the manual path
            }
            return 0;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
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
        return null; // installed layout: embedded template is authoritative, step skipped
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
