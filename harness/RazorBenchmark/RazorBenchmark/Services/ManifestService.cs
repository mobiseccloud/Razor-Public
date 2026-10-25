using RazorBenchmark.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RazorBenchmark.Services;

public static class ManifestService
{
    public static IReadOnlyList<CorpusEntry> Read(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var list = new List<CorpusEntry>();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var c = line.Split(',');
            if (c.Length < 10) throw new InvalidDataException($"Short manifest row: {line}");
            list.Add(new CorpusEntry(c[0], c[1], c[2], c[3], c[4],
                long.Parse(c[5]), c[6], c[7].Trim('"'), c[8], c[9]));
        }
        return list;
    }

    /// <summary>SHA-256 of a file, lowercase hex. Mismatch => reject, never accept silently.</summary>
    public static string Sha256Of(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    public static bool Verify(CorpusEntry entry, string localPath, out string actual)
    {
        actual = Sha256Of(localPath);
        return string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase)
            && new FileInfo(localPath).Length == entry.ExpectedBytes;
    }
}
