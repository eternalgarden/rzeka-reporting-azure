using RzekaReporting.Functions.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace RzekaReporting.Functions.Database;

public sealed class ReportsDbContext(DbContextOptions<ReportsDbContext> options) : DbContext(options)
{
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<CrashReport> CrashReports => Set<CrashReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Only what EF Core's conventions can't infer: keys, text lengths and the link between
        // the tables. Every other property becomes a column by convention.
        modelBuilder.Entity<Issue>(issue =>
        {
            issue.HasKey(i => i.Fingerprint);
            issue.Property(i => i.Fingerprint).HasMaxLength(64).IsFixedLength();
            issue.Property(i => i.Canonical).HasMaxLength(4000);
        });

        modelBuilder.Entity<CrashReport>(report =>
        {
            report.HasKey(r => r.ReportId);
            report.Property(r => r.Fingerprint).HasMaxLength(64).IsFixedLength();
            report.Property(r => r.AppName).HasMaxLength(200);
            report.Property(r => r.AppVersion).HasMaxLength(100);
            report
                .HasOne(r => r.Issue)
                .WithMany(i => i.Reports)
                .HasForeignKey(r => r.Fingerprint);
        });
    }
}
