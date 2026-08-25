using Microsoft.Extensions.DependencyInjection;

namespace FinancialStatementAI.Application;

/// <summary>
/// Composition root for this layer. Api/Worker call this once at startup instead of
/// registering Application-layer services themselves, so the hosts stay unaware of
/// which validators/handlers/services the Application layer registers internally.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
