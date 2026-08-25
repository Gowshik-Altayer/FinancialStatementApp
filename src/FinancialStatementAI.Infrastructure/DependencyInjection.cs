using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialStatementAI.Infrastructure;

/// <summary>
/// Composition root for this layer. Api/Worker call this once at startup instead of
/// registering Infrastructure-layer services (persistence, storage, OCR/AI, caching,
/// background jobs) themselves, keeping the hosts unaware of concrete implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        return services;
    }
}
