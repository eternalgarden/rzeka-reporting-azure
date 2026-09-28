using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RzekaReporting.Functions.Database;

// Used only by the `dotnet ef` tool (migrations), not by the running app.
// `dotnet ef database update` reads the real connection string from SQL_CONNECTION.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReportsDbContext>
{
    public ReportsDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<ReportsDbContext>()
                .UseSqlServer(
                    Environment.GetEnvironmentVariable("SQL_CONNECTION")
                        ?? "Server=design-time-only;Database=crashreports"
                )
                .Options
        );
}
