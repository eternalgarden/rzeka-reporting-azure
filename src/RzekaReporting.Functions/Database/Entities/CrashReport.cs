namespace RzekaReporting.Functions.Database.Entities;

// One row per report. Times are UTC.
public sealed class CrashReport
{
    public Guid ReportId { get; set; }
    public required string Fingerprint { get; set; }
    public Issue? Issue { get; set; }

    public DateTime OccurredAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public required string AppName { get; set; }
    public required string AppVersion { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? StackTrace { get; set; }

    // The full report JSON, so chain and breadcrumbs are kept without their own tables.
    public required string Payload { get; set; }
}
