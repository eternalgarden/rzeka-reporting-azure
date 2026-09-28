namespace RzekaReporting.Functions.Process;

// The backend's own view of the contract (contract/crash-report.v1.sample.json).
// Only what the processor needs is typed; chain and breadcrumbs stay in the raw JSON.
internal sealed record ReportMessage(
    int SchemaVersion,
    Guid ReportId,
    DateTimeOffset OccurredAt,
    ReportApp App,
    ReportFailure Failure
);

internal sealed record ReportApp(string Name, string Version);

internal sealed record ReportFailure(ReportSpell Spell, ReportException Exception);

internal sealed record ReportSpell(string Title, string School, string OwnerType, string? OwnerLabel);

internal sealed record ReportException(string Type, string? Message, string? StackTrace);
