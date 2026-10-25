namespace RazorBenchmark.Models;

/// <summary>One pinned corpus manifest entry (see CORPUS.md).</summary>
public sealed record CorpusEntry(
    string Suite,
    string Corpus,
    string File,
    string SourceUrl,
    string VersionDate,
    long ExpectedBytes,
    string Sha256,
    string License,
    string Provenance,
    string CacheLocation);
