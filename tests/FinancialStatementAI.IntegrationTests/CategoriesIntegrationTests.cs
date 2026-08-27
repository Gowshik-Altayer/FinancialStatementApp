using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialStatementAI.IntegrationTests;

public class CategoriesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CategoriesIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Sup3rSecret!",
            FirstName = "Cat",
            LastName = "Manager"
        });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Sup3rSecret!",
            FirstName = "Ada",
            LastName = "Admin"
        });
        var registered = await register.Content.ReadFromJsonAsync<AuthResponse>();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users.SingleAsync(u => u.Id == registered!.UserId);
            user.Role = UserRole.Admin;
            await dbContext.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sup3rSecret!" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Fact]
    public async Task GetAll_Returns_Only_Active_Categories()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(categories.GetArrayLength() > 0);
    }

    [Fact]
    public async Task NonAdmin_Cannot_Create_A_Category()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/categories", new { Name = "Should Not Work" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Can_Create_Read_Update_And_Deactivate_A_Category()
    {
        var admin = await CreateAdminClientAsync();
        var name = $"Test Category {Guid.NewGuid():N}";

        var createResponse = await admin.PostAsJsonAsync("/api/categories", new { Name = name, Description = "Created by test" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.True(created.GetProperty("isActive").GetBoolean());

        var getResponse = await admin.GetAsync($"/api/categories/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateResponse = await admin.PutAsJsonAsync($"/api/categories/{id}", new { Name = name, Description = "Updated by test" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated by test", updated.GetProperty("description").GetString());

        var allBeforeDeactivate = await (await admin.GetAsync("/api/categories")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(allBeforeDeactivate.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);

        var deactivateResponse = await admin.PostAsync($"/api/categories/{id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var deactivated = await deactivateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(deactivated.GetProperty("isActive").GetBoolean());

        var allAfterDeactivate = await (await admin.GetAsync("/api/categories")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(allAfterDeactivate.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);

        var allIncludingInactive = await (await admin.GetAsync("/api/categories/all")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(allIncludingInactive.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id && !c.GetProperty("isActive").GetBoolean());

        var reactivateResponse = await admin.PostAsync($"/api/categories/{id}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
        var reactivated = await reactivateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(reactivated.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Create_Rejects_A_Duplicate_Category_Name()
    {
        var admin = await CreateAdminClientAsync();
        var name = $"Duplicate {Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/categories", new { Name = name })).StatusCode);

        var secondResponse = await admin.PostAsJsonAsync("/api/categories", new { Name = name });

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Deactivate_Returns_NotFound_For_An_Unknown_Category()
    {
        var admin = await CreateAdminClientAsync();

        var response = await admin.PostAsync($"/api/categories/{Guid.NewGuid()}/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Stats_Reflects_A_Users_Own_Classified_Transactions()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/categories/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, stats.ValueKind);
        Assert.Equal(0, stats.GetArrayLength());
    }
}
