using Microsoft.EntityFrameworkCore;
using RzekaReporting.Functions.Database.Entities;

namespace RzekaReporting.Functions.Database;

public enum SaveOutcome
{
    Saved,
    Duplicate,
}

public sealed class ReportStore(ReportsDbContext db)
{
    public async Task<SaveOutcome> SaveAsync(
        CrashReport report,
        string canonical,
        CancellationToken cancellationToken = default
    )
    {
        // Service Bus may deliver a message more than once; the ReportId makes that harmless.
        if (await db.CrashReports.AnyAsync(r => r.ReportId == report.ReportId, cancellationToken))
            return SaveOutcome.Duplicate;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Counted inside the database (Count = Count + 1), so two processors updating the
        // same issue at once can't overwrite each other's increment.
        int updated = await db
            .Issues.Where(i => i.Fingerprint == report.Fingerprint)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(i => i.Count, i => i.Count + 1)
                        .SetProperty(
                            i => i.FirstSeen,
                            i => i.FirstSeen < report.OccurredAt ? i.FirstSeen : report.OccurredAt
                        )
                        .SetProperty(
                            i => i.LastSeen,
                            i => i.LastSeen > report.OccurredAt ? i.LastSeen : report.OccurredAt
                        ),
                cancellationToken
            );

        // First Issue
        if (updated == 0)
            db.Issues.Add(
                new Issue
                {
                    Fingerprint = report.Fingerprint,
                    Canonical = canonical,
                    Count = 1,
                    FirstSeen = report.OccurredAt,
                    LastSeen = report.OccurredAt,
                }
            );

        db.CrashReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaveOutcome.Saved;
    }
}
