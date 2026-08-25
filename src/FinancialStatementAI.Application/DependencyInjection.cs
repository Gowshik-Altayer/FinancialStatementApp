using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FluentValidation;
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
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IStatementService, StatementService>();

        return services;
    }
}
