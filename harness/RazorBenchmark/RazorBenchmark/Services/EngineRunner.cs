using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RazorBenchmark.Services;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, long ElapsedMs, bool TimedOut);

public static class EngineRunner
{
    /// <summary>
    /// Runs an external engine process with a timeout. Never downloads anything.
    /// Missing executable => (exit -1, timedOut false) so callers show "unavailable".
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string exe, string args, string workDir, int timeoutMs, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(exe) || !ExistsOnPath(exe))
            return new ProcessResult(-1, "", "executable not found: " + exe, 0, false);
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = string.IsNullOrWhiteSpace(workDir) ? Environment.CurrentDirectory : workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        var sw = Stopwatch.StartNew();
        proc.Start();
        var outTask = proc.StandardOutput.ReadToEndAsync();
        var errTask = proc.StandardError.ReadToEndAsync();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(true); } catch { }
            return new ProcessResult(proc.ExitCode, await outTask, await errTask, sw.ElapsedMilliseconds, true);
        }
        sw.Stop();
        return new ProcessResult(proc.ExitCode, await outTask, await errTask, sw.ElapsedMilliseconds, false);
    }

    public static bool ExistsOnPath(string exe)
    {
        try
        {
            if (Path.IsPathRooted(exe) || exe.Contains(Path.DirectorySeparatorChar))
                return File.Exists(exe);
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                try
                {
                    var full = Path.Combine(dir.Trim('"'), exe);
                    if (File.Exists(full) || File.Exists(full + ".exe")) return true;
                }
                catch { }
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>
    /// Runs a shell command string (engine arg templates support shell
    /// redirection). Windows: <c>cmd /c</c>; Unix: <c>/bin/sh -c</c>.
    /// Same timeout/kill/cancel contract as <see cref="RunAsync"/>.
    /// </summary>
    public static Task<ProcessResult> RunShellAsync(
        string command, string workDir, int timeoutMs, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
            return RunAsync("cmd", "/c " + command, workDir, timeoutMs, ct);
        return RunAsync("/bin/sh", "-c " + command, workDir, timeoutMs, ct);
    }

    /// <summary>
    /// Runs an engine invocation: executable plus an already-substituted arg
    /// template, through the platform shell. Executables containing spaces
    /// are quoted (e.g. commercial tools under Program Files).
    /// </summary>
    public static Task<ProcessResult> RunShellAsync(
        string exe, string templatedArgs, string workDir,
        int timeoutMs, CancellationToken ct)
    {
        var q = exe.Contains(' ') && !exe.StartsWith('"') ? "\"" + exe + "\"" : exe;
        return RunShellAsync(q + " " + templatedArgs, workDir, timeoutMs, ct);
    }
}
