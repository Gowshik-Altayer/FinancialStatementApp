using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Infrastructure.Persistence;
using FinancialStatementAI.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialStatementAI.IntegrationTests;

/// <summary>Same InMemory-database/temp-storage swap as CustomWebApplicationFactory, but also
/// replaces ICategoryRepository with a stub that always throws — the only way to genuinely
/// exercise GlobalExceptionHandler through the real HTTP pipeline (Program.cs's
/// app.UseExceptionHandler(), not just the handler class in isolation) without adding a
/// throw-on-purpose endpoint into production controllers.</summary>
public class GlobalExceptionHandlerWebApplicationFactory : WebApplicationFactory<FinancialStatementAI.Api.Program>
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

            services.RemoveAll<ICategoryRepository>();
            services.AddScoped<ICategoryRepository, AlwaysThrowingCategoryRepository>();
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

    private class AlwaysThrowingCategoryRepository : ICategoryRepository
    {
        public Task<IReadOnlyList<Domain.Entities.Category>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated failure for GlobalExceptionHandler testing.");

        public Task<Domain.Entities.Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated failure for GlobalExceptionHandler testing.");
    }
}
