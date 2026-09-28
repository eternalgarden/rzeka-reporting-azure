namespace RzekaReporting.Functions.Database;

// One row per bug: all reports with the same fingerprint.
public sealed class Issue
{
    public required string Fingerprint { get; set; }
    public required string Canonical { get; set; }
    public int Count { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }

    public List<CrashReport> Reports { get; set; } = [];
}
