using FinancialStatementAI.Infrastructure.Persistence;
using FinancialStatementAI.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialStatementAI.IntegrationTests;

/// <summary>Swaps the real SQL Server DbContext registration for a fresh, per-factory-instance
/// EF Core InMemory database, and redirects local file storage to a temp folder, so integration
/// tests run fully self-contained (no SQL Server dependency, no writes into the repo's own
/// App_Data folder, no shared state between test classes).</summary>
public class CustomWebApplicationFactory : WebApplicationFactory<FinancialStatementAI.Api.Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly string _uploadsPath = Path.Combine(Path.GetTempPath(), "fsai-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.Configure<LocalFileStorageOptions>(options => options.RootPath = _uploadsPath);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (Directory.Exists(_uploadsPath))
        {
            Directory.Delete(_uploadsPath, recursive: true);
        }
    }
}
