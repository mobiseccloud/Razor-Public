using System.Collections.Generic;

namespace RazorBenchmark.Models;

/// <summary>
/// An external engine adapter. Engines are spawned as child processes only;
/// the harness never links proprietary code.
/// CompressArgs/DecompressArgs/VersionArgs are aspirational templates — no
/// substitution engine or version probe consumes them yet (the --engines probe
/// only checks PATH presence). Do not treat them as executed command lines.
/// Razor entries carry no VersionArgs: the quarantined razor-bench.exe has no
/// --version flag (it panics on unknown args), and probing an unverified flag
/// is forbidden.
/// </summary>
public sealed record EngineSpec(
    string Name,
    string Tier,
    string Executable,
    string CompressArgs,
    string DecompressArgs,
    string VersionArgs,
    string InstallHelpUrl)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Executable);
}

public static class EngineCatalog
{
    public static IReadOnlyList<EngineSpec> Defaults() => new[]
    {
        new EngineSpec("Razor", "Max", "", "{input} -> {output}", "{input} -> {output}", "",
            "Commercial binary — no path configuration UI exists yet; nothing is auto-downloaded."),
        new EngineSpec("Razor", "MaxFast", "", "{input} -> {output}", "{input} -> {output}", "",
            "Commercial binary — no path configuration UI exists yet."),
        new EngineSpec("Razor", "Fast", "", "{input} -> {output}", "{input} -> {output}", "",
            "Commercial binary — no path configuration UI exists yet."),
        new EngineSpec("brotli", "q11", "brotli", "--quality 11 --input {input} --output {output}", "--decompress --input {input} --output {output}", "--version",
            "https://github.com/google/brotli"),
        new EngineSpec("zstd", "19", "zstd", "-19 -c {input} -o {output}", "-d -c {input} -o {output}", "--version",
            "https://github.com/facebook/zstd"),
        new EngineSpec("xz", "9e", "xz", "-9e -c {input} > {output}", "-d -c {input} > {output}", "--version",
            "https://tukaani.org/xz/"),
        new EngineSpec("gzip", "default", "gzip", "-c {input} > {output}", "-d -c {input} > {output}", "--version",
            "https://www.gnu.org/software/gzip/"),
        new EngineSpec("deflate", "default", "", "", "", "",
            "Use gzip/zlib tooling; kept as a separate label for published comparison columns."),
    };
}
