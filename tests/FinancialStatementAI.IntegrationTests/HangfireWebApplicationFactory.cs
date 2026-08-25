using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Infrastructure.BackgroundJobs;
using FinancialStatementAI.Infrastructure.Persistence;
using FinancialStatementAI.Infrastructure.Storage;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialStatementAI.IntegrationTests;

/// <summary>Same InMemory-database/temp-storage swap as CustomWebApplicationFactory, but also
/// swaps in the real Hangfire-backed IBackgroundJobScheduler (with Hangfire's own in-process
/// storage, not SQL Server) instead of the default synchronous one — so this one factory can
/// verify the real Hangfire wiring (a job actually gets enqueued in Hangfire's own storage)
/// without needing a SQL Server instance. Deliberately does NOT start a Hangfire processing
/// server (no AddHangfireServer()) — these tests assert against the queued job itself, not its
/// eventual (asynchronous, timing-dependent) completion.
///
/// Note this is done via ConfigureServices, not by injecting a "BackgroundJobs:Provider"
/// config value: Program.cs's Infrastructure.DependencyInjection.AddInfrastructure call reads
/// that config key and decides which scheduler to register *before* WebApplicationFactory's
/// ConfigureWebHost customizations are merged in (they only take effect once the deferred host
/// builder's Build() call runs), so a config override added here would arrive too late for that
/// decision — ConfigureServices runs against the already-built IServiceCollection and can freely
/// replace whatever AddInfrastructure already registered.</summary>
public class HangfireWebApplicationFactory : WebApplicationFactory<FinancialStatementAI.Api.Program>
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

            services.RemoveAll<IBackgroundJobScheduler>();
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseInMemoryStorage());
            services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();
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
