using RazorBenchmark.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RazorBenchmark.Services;

public static class CsvService
{
    public const string RequiredHeader =
        "suite,corpus,file,engine,tier,original_bytes,compressed_bytes,ratio_percent," +
        "encode_ms,decode_ms,encode_mb_s,decode_mb_s,round_trip,status,timestamp_utc," +
        "host,threads,binary_version,format_version";

    public static readonly HashSet<string> ValidStatus = new(StringComparer.Ordinal)
        { "PASS", "RT-FAIL", "ERROR", "TIMEOUT", "SKIPPED" };

    public static IReadOnlyList<BenchmarkRow> Read(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length < 1 || lines[0].Trim() != RequiredHeader)
            throw new InvalidDataException($"Bad CSV header in {path}");
        var rows = new List<BenchmarkRow>();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var c = Split(line);
            if (c.Length < 19) throw new InvalidDataException($"Short row in {path}: {line}");
            if (!ValidStatus.Contains(c[13]))
                throw new InvalidDataException($"Bad status '{c[13]}' in {path}");
            rows.Add(new BenchmarkRow(c[0], c[1], c[2], c[3], c[4],
                long.Parse(c[5], CultureInfo.InvariantCulture),
                long.Parse(c[6], CultureInfo.InvariantCulture),
                double.Parse(c[7], CultureInfo.InvariantCulture),
                long.Parse(c[8], CultureInfo.InvariantCulture),
                long.Parse(c[9], CultureInfo.InvariantCulture),
                double.Parse(c[10], CultureInfo.InvariantCulture),
                double.Parse(c[11], CultureInfo.InvariantCulture),
                c[12], c[13], c[14], c[15],
                int.Parse(c[16], CultureInfo.InvariantCulture), c[17], c[18]));
        }
        return rows;
    }

    public static void Write(string path, IEnumerable<BenchmarkRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(RequiredHeader);
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",", r.Suite, r.Corpus, r.File, r.Engine, r.Tier,
                r.OriginalBytes.ToString(CultureInfo.InvariantCulture),
                r.CompressedBytes.ToString(CultureInfo.InvariantCulture),
                r.RatioPercent.ToString(CultureInfo.InvariantCulture),
                r.EncodeMs.ToString(CultureInfo.InvariantCulture),
                r.DecodeMs.ToString(CultureInfo.InvariantCulture),
                r.EncodeMbS.ToString(CultureInfo.InvariantCulture),
                r.DecodeMbS.ToString(CultureInfo.InvariantCulture),
                r.RoundTrip, r.Status, r.TimestampUtc, r.Host,
                r.Threads.ToString(CultureInfo.InvariantCulture),
                r.BinaryVersion, r.FormatVersion));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string[] Split(string line) => line.Split(',');
}
