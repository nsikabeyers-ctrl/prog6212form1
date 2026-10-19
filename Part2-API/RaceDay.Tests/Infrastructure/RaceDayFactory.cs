using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests.Infrastructure;

/// <summary>
/// Starts the real API in memory for the tests. The SQL Server database is swapped for an
/// in-memory one, so the tests need no database and run the same locally and on GitHub Actions.
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    // Same value as appsettings.json, and pinned again below so the tests do not depend on that file.
    public const string OrganiserInviteCode = "RACEDAY-ORGANISER-2026";

    private readonly string _databaseName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:OrganiserInviteCode"] = OrganiserInviteCode
            });
        });

        builder.ConfigureServices(services =>
        {
            var existing = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            if (existing is not null)
            {
                services.Remove(existing);
            }

            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
