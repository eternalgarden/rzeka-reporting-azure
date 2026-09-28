using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RzekaReporting.Functions.Database;

namespace RzekaReporting.Tests;

// Runs against SQLite in memory, not Azure SQL: fast and needs no cloud, but a different
// database engine. Testing against the real engine (Testcontainers) is on the roadmap.
public sealed class ReportStoreTests : IDisposable
{
    const string BugA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string BugB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    static readonly DateTime Noon = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ReportStoreTests()
    {
        _connection.Open();
        using ReportsDbContext db = NewContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    // A new context per operation, like one per function run in the real app.
    ReportsDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ReportsDbContext>().UseSqlite(_connection).Options);

    async Task<SaveOutcome> Save(CrashReport report)
    {
        using ReportsDbContext db = NewContext();
        return await new ReportStore(db).SaveAsync(report: report, canonical: $"v1|{report.Fingerprint}");
    }

    static CrashReport Report(string fingerprint, DateTime? occurredAt = null, Guid? id = null) =>
        new()
        {
            ReportId = id ?? Guid.NewGuid(),
            Fingerprint = fingerprint,
            OccurredAt = occurredAt ?? Noon,
            ReceivedAt = Noon,
            AppName = "tests",
            AppVersion = "1.0.0",
            Payload = "{}",
        };

    Issue SingleIssue(string fingerprint)
    {
        using ReportsDbContext db = NewContext();
        return db.Issues.Single(i => i.Fingerprint == fingerprint);
    }

    [Fact]
    public async Task First_report_creates_its_issue()
    {
        Assert.Equal(SaveOutcome.Saved, await Save(Report(BugA)));

        Issue issue = SingleIssue(BugA);
        Assert.Equal(1, issue.Count);
        Assert.Equal("v1|" + BugA, issue.Canonical);
        Assert.Equal(Noon, issue.FirstSeen);
        Assert.Equal(Noon, issue.LastSeen);
    }

    [Fact]
    public async Task Same_fingerprint_groups_under_one_issue()
    {
        await Save(Report(BugA));
        await Save(Report(BugA));

        Assert.Equal(2, SingleIssue(BugA).Count);
        using ReportsDbContext db = NewContext();
        Assert.Equal(2, db.CrashReports.Count());
        Assert.Equal(1, db.Issues.Count());
    }

    [Fact]
    public async Task Different_fingerprints_are_different_issues()
    {
        await Save(Report(BugA));
        await Save(Report(BugB));

        using ReportsDbContext db = NewContext();
        Assert.Equal(2, db.Issues.Count());
    }

    [Fact]
    public async Task Same_report_delivered_twice_is_stored_once()
    {
        Guid id = Guid.NewGuid();

        Assert.Equal(SaveOutcome.Saved, await Save(Report(BugA, id: id)));
        Assert.Equal(SaveOutcome.Duplicate, await Save(Report(BugA, id: id)));

        Assert.Equal(1, SingleIssue(BugA).Count);
        using ReportsDbContext db = NewContext();
        Assert.Equal(1, db.CrashReports.Count());
    }

    [Fact]
    public async Task First_and_last_seen_follow_occurrence_time_not_arrival_order()
    {
        await Save(Report(BugA, occurredAt: Noon));
        await Save(Report(BugA, occurredAt: Noon.AddHours(2)));
        await Save(Report(BugA, occurredAt: Noon.AddHours(-3))); // arrives last, happened first

        Issue issue = SingleIssue(BugA);
        Assert.Equal(Noon.AddHours(-3), issue.FirstSeen);
        Assert.Equal(Noon.AddHours(2), issue.LastSeen);
    }
}
