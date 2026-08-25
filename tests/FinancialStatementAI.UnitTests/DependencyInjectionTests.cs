using FinancialStatementAI.Application;
using FinancialStatementAI.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialStatementAI.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_And_AddInfrastructure_Build_A_Resolvable_Container()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider);
    }
}
