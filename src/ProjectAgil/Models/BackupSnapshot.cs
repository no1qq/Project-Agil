namespace ProjectAgil.Models;

public sealed class BackupEntry
{
    public string TweakId { get; set; } = string.Empty;

    public string TweakName { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string? PreviousValue { get; set; }

    public bool Existed { get; set; }

    public string AppliedValue { get; set; } = string.Empty;

    public string? AdapterId { get; set; }

    public string? AdapterName { get; set; }

    public bool Restored { get; set; }

    public string? RestoreError { get; set; }

    public string PreviousDisplay => Existed ? PreviousValue ?? string.Empty : "not set";

    public string StatusDisplay =>
        Restored ? "Put back"
        : RestoreError is null ? "Active"
        : "Failed";
}

public sealed class BackupSnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public string Label { get; set; } = string.Empty;

    public string ProfileName { get; set; } = string.Empty;

    public string AdapterName { get; set; } = string.Empty;

    public bool Restored { get; set; }

    public List<BackupEntry> Entries { get; set; } = [];

    public BenchmarkComparison? Benchmark { get; set; }

    public DateTime CreatedLocal => CreatedUtc.ToLocalTime();

    public string CreatedDisplay => CreatedLocal.ToString("dd MMM yyyy  HH:mm:ss", CultureInfo.InvariantCulture);

    public int EntryCount => Entries.Count;

    public int RestoredCount => Restored ? Entries.Count : Entries.Count(e => e.Restored);

    public int PendingCount => Entries.Count - RestoredCount;

    public bool IsFullyRestored => Restored || (Entries.Count > 0 && Entries.All(e => e.Restored));

    public bool IsPartlyRestored => !IsFullyRestored && Entries.Any(e => e.Restored);

    public string StatusDisplay =>
        IsFullyRestored ? "Reverted"
        : IsPartlyRestored ? $"Partly reverted ({RestoredCount} of {EntryCount})"
        : "Active";
}
