using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RzekaReporting.Functions.Database;

// Used only by the `dotnet ef` tool (migrations), not by the running app.
// `dotnet ef database update` reads the real connection string from the environment variable
// SqlConnection (the same name as the app setting the running app uses).
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReportsDbContext>
{
    public ReportsDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<ReportsDbContext>()
                .UseSqlServer(
                    connectionString: Environment.GetEnvironmentVariable("SqlConnection")
                        ?? "Server=design-time-only;Database=crashreports",
                    // A sleeping serverless database refuses the first connection (error 40613)
                    // while it wakes up; retry instead of failing. Tool only: the running app
                    // leaves retries to Service Bus (ADR 0001).
                    sqlServerOptionsAction: sql =>
                        sql.EnableRetryOnFailure(
                            maxRetryCount: 6,
                            maxRetryDelay: TimeSpan.FromSeconds(20),
                            errorNumbersToAdd: null
                        )
                )
                .Options
        );
}
