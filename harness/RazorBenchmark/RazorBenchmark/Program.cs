using Avalonia;
using RazorBenchmark.Models;
using RazorBenchmark.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading;

namespace RazorBenchmark;

internal sealed class Program
{
    /// <summary>
    /// Headless entry points (CI / smoke / headless runs): --csvcheck
    /// &lt;path&gt; reloads a standard-schema CSV; --engines lists engine
    /// availability; --selftest runs the offline gate battery (CSV, hash,
    /// trial encoding, manifest parse, matrix fakes, comparison math);
    /// --run &lt;manifest&gt; --out &lt;csv&gt; executes the one-click
    /// matrix over verified cached files. Otherwise starts the GUI.
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
                // 4. Manifest parse (both shipped schemas) + local Verify
                // (no network: temp files only).
                var manifest1 = FindInAncestors(System.IO.Path.Combine("docs", "benchmarks", "manifest-suite1.csv"));
                var manifest2 = FindInAncestors(System.IO.Path.Combine("docs", "benchmarks", "manifest-suite2.csv"));
                if (manifest1 is not null)
                {
                    var entries = Services.ManifestService.Read(manifest1);
                    if (entries.Count == 0) throw new InvalidOperationException("suite1 manifest empty");
                }
                if (manifest2 is not null)
                {
                    var entries = Services.ManifestService.Read(manifest2);
                    if (entries.Count == 0) throw new InvalidOperationException("suite2 manifest empty");
                }
                {
                    var probe = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        "razorbench-selftest-" + Guid.NewGuid().ToString("N") + ".bin");
                    System.IO.File.WriteAllBytes(probe, new byte[] { 9, 8, 7 });
                    var entry = new Models.CorpusEntry("s", "c", "f", "u", "d",
                        3, Services.ManifestService.Sha256Of(probe),
                        "l", "p", "%CORPUS_CACHE%/s/c/f");
                    if (!Services.ManifestService.Verify(entry, probe, out _))
                        throw new InvalidOperationException("ManifestService.Verify rejected a good file");
                    System.IO.File.WriteAllBytes(probe, new byte[] { 9, 8, 6 });
                    if (Services.ManifestService.Verify(entry, probe, out _))
                        throw new InvalidOperationException("ManifestService.Verify accepted a tampered file");
                    System.IO.File.Delete(probe);
                    var resolved = Services.ManifestService.ResolveCachePath("%CORPUS_CACHE%/s/c/f");
                    if (!resolved.EndsWith(System.IO.Path.Combine("s", "c", "f")))
                        throw new InvalidOperationException("cache path resolution wrong: " + resolved);
                }
                // 5. RunMatrix with fake engines (no network, no real engines):
                // copy-fake round-trips (PASS), garbage-fake fails closed
                // (RT-FAIL, never dropped), missing exe counts SKIPPED, and
                // the --selftest-sleep hook proves timeout handling.
                {
                    var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        "razorbench-selftest-" + Guid.NewGuid().ToString("N"));
                    System.IO.Directory.CreateDirectory(dir);
                    try
                    {
                        var input = System.IO.Path.Combine(dir, "in.bin");
                        System.IO.File.WriteAllBytes(input, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
                        var isWin = OperatingSystem.IsWindows();
                        var copyExe = isWin ? "cmd" : "sh";
                        var copyArgs = isWin ? "/c copy /y" : "-c cp";
                        var garbageArgs = isWin ? "/c echo garbage >" : "-c \"printf garbage >";
                        var copySpec = new Models.EngineSpec("fake", "copy", copyExe,
                            copyArgs + " \"{input}\" \"{output}\"",
                            copyArgs + " \"{input}\" \"{output}\"", "", "");
                        var garbageSpec = new Models.EngineSpec("fake", "garbage", copyExe,
                            garbageArgs + " \"{output}\"",
                            garbageArgs + " \"{output}\"", "", "");
                        var cts = new CancellationTokenSource();
                        var rowsPG = Services.BenchmarkService.RunMatrixAsync(
                            "selftest", "selftest", new[] { input },
                            new[] { copySpec, garbageSpec },
                            _ => copyExe, "selftest", 1, "selftest",
                            null, cts.Token, timeoutMsPerCell: 30_000)
                            .GetAwaiter().GetResult();
                        var t = Services.BenchmarkService.LastTotals;
                        if (t.Passed != 1 || t.RoundTripFailures != 1 || rowsPG.Count != 2)
                            throw new InvalidOperationException(
                                $"matrix fake engines wrong: pass={t.Passed} rtfail={t.RoundTripFailures} rows={rowsPG.Count}");
                        if (!rowsPG.All(r => r.Status == "PASS" || r.Status == "RT-FAIL"))
                            throw new InvalidOperationException("unexpected matrix row status");
                        // Missing executable => SKIPPED count, no row.
                        var missingSpec = new Models.EngineSpec("fake", "missing",
                            "", "x {input} {output}", "x {input} {output}", "", "");
                        rowsPG = Services.BenchmarkService.RunMatrixAsync(
                            "selftest", "selftest", new[] { input },
                            new[] { missingSpec },
                            _ => (string?)null, "selftest", 1, "selftest",
                            null, cts.Token).GetAwaiter().GetResult();
                        if (rowsPG.Count != 0 || Services.BenchmarkService.LastTotals.Skipped != 1)
                            throw new InvalidOperationException("missing engine was not counted SKIPPED");
                        // Timeout via the --selftest-sleep hook. Resolve our own
                        // binary robustly: under `dotnet run` the process
                        // image is the dotnet host, so prefer the apphost
                        // next to the running assembly when present.
                        var hookExe = System.IO.Path.Combine(
                            System.AppContext.BaseDirectory,
                            "RazorBenchmark" + (OperatingSystem.IsWindows() ? ".exe" : ""));
                        if (!System.IO.File.Exists(hookExe))
                            hookExe = Environment.ProcessPath ?? throw new InvalidOperationException("no process path");
                        var sleepSpec = new Models.EngineSpec("fake", "sleep",
                            hookExe, "--selftest-sleep 30000", "--selftest-sleep 0", "", "");
                        rowsPG = Services.BenchmarkService.RunMatrixAsync(
                            "selftest", "selftest", new[] { input },
                            new[] { sleepSpec },
                            _ => hookExe, "selftest", 1, "selftest",
                            null, cts.Token, timeoutMsPerCell: 1500).GetAwaiter().GetResult();
                        if (Services.BenchmarkService.LastTotals.Timeouts != 1 || rowsPG.Count != 0)
                            throw new InvalidOperationException("sleep hook was not counted TIMEOUT");
                        // Comparison math sanity.
                        var measured = new[]
                        {
                            new Models.BenchmarkRow("s","c","f","e","t",100,25,25.0,1,1,1,1,"OK","PASS","t","h",1,"b","archival"),
                        };
                        var published = new[]
                        {
                            new Models.BenchmarkRow("s","c","f","e","t",100,30,30.0,1,1,1,1,"OK","PASS","t","h",1,"b","archival"),
                        };
                        var summary = Services.BenchmarkService.CompareWithPublished(measured, published);
                        if (!summary.Contains("Compared 1 rows") || !summary.Contains("5"))
                            throw new InvalidOperationException("comparison math wrong: " + summary);
                    }
                    finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
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
        if (args.Length == 2 && args[0] == "--selftest-sleep")
        {
            // Test-only hook for EngineRunner timeout coverage (used by
            // --selftest). Sleeps, then exits 0. Never used by benchmarks.
            if (int.TryParse(args[1], out var ms) && ms >= 0 && ms <= 120_000)
            {
                System.Threading.Thread.Sleep(ms);
                return 0;
            }
            Console.Error.WriteLine("--selftest-sleep needs 0..120000 ms");
            return 1;
        }
        if (args.Length >= 2 && args[0] == "--run")
        {
            // Headless one-click matrix: --run <manifestCsv> --out <outCsv>.
            // Same engine as the GUI Run button (verified cached files only,
            // unavailable engines counted as skipped, never fabricated).
            try
            {
                return RunHeadless(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("RUN FAIL: " + ex.Message);
                return 1;
            }
        }
        if (args.Length == 1 && args[0].StartsWith("--fetch-manifest=", StringComparison.Ordinal))
        {
            // Headless corpus provisioning: downloads + verifies every entry.
            // Same engine as the GUI Download button. Exit 0 only when zero
            // failures (local-only skips are fine); failures are listed.
            try
            {
                var manifest = args[0].Substring("--fetch-manifest=".Length);
                var summary = Services.DownloaderService.FetchManifestAsync(
                    manifest,
                    entry =>
                    {
                        var dest = Services.ManifestService.ResolveCachePath(entry.CacheLocation);
                        return System.IO.Path.GetDirectoryName(dest)!;
                    },
                    new Progress<(Models.CorpusEntry, Services.DownloadProgress)>(t =>
                        Console.WriteLine($"  {t.Item1.File}: {t.Item2.BytesReceived}/{t.Item2.TotalBytes ?? t.Item1.ExpectedBytes} [{t.Item2.State}]")),
                    3, CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine($"FETCH OK: {summary.Verified} verified, "
                    + $"{summary.LocalOnlySkipped} local-only skipped, "
                    + $"{summary.Failed.Count} failed.");
                foreach (var f in summary.Failed) Console.WriteLine("  FAIL: " + f);
                return summary.Failed.Count == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FETCH FAIL: " + ex.Message);
                return 1;
            }
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    private static int RunHeadless(string[] args)
    {
        string? manifest = null;
        string? outCsv = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--out" && i + 1 < args.Length) outCsv = args[++i];
            else if (!args[i].StartsWith("--", StringComparison.Ordinal)) manifest = args[i];
            else throw new InvalidOperationException($"unknown flag {args[i]} (usage: --run <manifestCsv> --out <outCsv>)");
        }
        if (manifest is null || outCsv is null)
            throw new InvalidOperationException("usage: --run <manifestCsv> --out <outCsv>");
        var entries = Services.ManifestService.Read(manifest);
        var suite = entries.Count > 0 ? entries[0].Suite : "suite";
        var groups = entries
            .Select(e => (Entry: e, Local: Services.ManifestService.ResolveCachePath(e.CacheLocation)))
            .Where(x => System.IO.File.Exists(x.Local)
                && Services.ManifestService.Verify(x.Entry, x.Local, out _))
            .GroupBy(x => x.Entry.Corpus)
            .ToList();
        var missing = entries.Count - groups.Sum(g => g.Count());
        if (groups.Count == 0)
            throw new InvalidOperationException(
                $"no verified corpus files cached ({missing} missing). Download the manifest entries first.");
        var engines = Models.EngineCatalog.Defaults();
        var allRows = new List<Models.BenchmarkRow>();
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        foreach (var group in groups)
        {
            var rows = Services.BenchmarkService.RunMatrixAsync(
                suite, group.Key,
                group.Select(x => x.Local).ToList(),
                engines, Services.BenchmarkService.ResolveExecutable,
                Environment.MachineName, 1, "harness-cli",
                progress: null, cts.Token).GetAwaiter().GetResult();
            allRows.AddRange(rows);
            var t = Services.BenchmarkService.LastTotals;
            Console.WriteLine($"{group.Key}: {rows.Count} rows "
                + $"(pass {t.Passed}, rt-fail {t.RoundTripFailures}, "
                + $"error {t.Errors}, timeout {t.Timeouts}, skipped {t.Skipped})");
        }
        Services.CsvService.Write(outCsv, allRows);
        Console.WriteLine($"RUN OK: {allRows.Count} rows -> {outCsv}");
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
