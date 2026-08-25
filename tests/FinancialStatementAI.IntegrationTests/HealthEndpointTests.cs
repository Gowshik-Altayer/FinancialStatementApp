using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialStatementAI.IntegrationTests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<FinancialStatementAI.Api.Program>>
{
    private readonly WebApplicationFactory<FinancialStatementAI.Api.Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<FinancialStatementAI.Api.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
