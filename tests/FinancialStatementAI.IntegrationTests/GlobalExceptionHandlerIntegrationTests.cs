using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialStatementAI.IntegrationTests;

/// <summary>Verifies GlobalExceptionHandler through the real HTTP pipeline (Program.cs's
/// app.UseExceptionHandler()), not just the handler class in isolation.</summary>
public class GlobalExceptionHandlerIntegrationTests : IClassFixture<GlobalExceptionHandlerWebApplicationFactory>
{
    private readonly GlobalExceptionHandlerWebApplicationFactory _factory;

    public GlobalExceptionHandlerIntegrationTests(GlobalExceptionHandlerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"{Guid.NewGuid():N}@example.com",
            Password = "Sup3rSecret!",
            FirstName = "Ada",
            LastName = "Lovelace"
        });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Fact]
    public async Task An_Unhandled_Exception_Returns_A_Generic_500_ProblemDetails_Response()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Simulated failure", body);
        Assert.DoesNotContain("AlwaysThrowingCategoryRepository", body);

        using var json = JsonDocument.Parse(body);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task An_Unhandled_Exception_Is_Persisted_To_The_ExceptionLogs_Table()
    {
        var client = await CreateAuthenticatedClientAsync();

        await client.GetAsync("/api/categories");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logged = await dbContext.ExceptionLogs
            .OrderByDescending(e => e.OccurredAt)
            .FirstOrDefaultAsync(e => e.RequestPath == "/api/categories");

        Assert.NotNull(logged);
        Assert.Contains("InvalidOperationException", logged!.ExceptionType);
        Assert.Equal("Simulated failure for GlobalExceptionHandler testing.", logged.Message);
        Assert.Equal("GET", logged.RequestMethod);
        Assert.Equal(500, logged.StatusCode);
        Assert.NotNull(logged.UserId);
    }
}
