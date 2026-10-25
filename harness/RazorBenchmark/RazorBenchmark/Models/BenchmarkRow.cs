namespace RazorBenchmark.Models;

/// <summary>
/// One row of the public benchmark CSV schema (see README §6 / BENCHMARKS.md).
/// </summary>
public sealed record BenchmarkRow(
    string Suite,
    string Corpus,
    string File,
    string Engine,
    string Tier,
    long OriginalBytes,
    long CompressedBytes,
    double RatioPercent,
    long EncodeMs,
    long DecodeMs,
    double EncodeMbS,
    double DecodeMbS,
    string RoundTrip,
    string Status,
    string TimestampUtc,
    string Host,
    int Threads,
    string BinaryVersion,
    string FormatVersion);
