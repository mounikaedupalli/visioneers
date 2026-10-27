using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhishGuard.Data;

/// <summary>Used by `dotnet ef` only. Override with env var PHISHGUARD_SQL.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PhishGuardDbContext>
{
    public const string DefaultConnection =
        @"Server=(localdb)\MSSQLLocalDB;Database=PhishGuard;Trusted_Connection=True;TrustServerCertificate=True";

    public PhishGuardDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("PHISHGUARD_SQL") ?? DefaultConnection;
        var options = new DbContextOptionsBuilder<PhishGuardDbContext>().UseSqlServer(cs).Options;
        return new PhishGuardDbContext(options);
    }
}
