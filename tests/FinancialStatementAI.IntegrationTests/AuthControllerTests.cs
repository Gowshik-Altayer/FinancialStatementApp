using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static RegisterRequest NewRegisterRequest() => new()
    {
        Email = $"{Guid.NewGuid():N}@example.com",
        Password = "Sup3rSecret!",
        FirstName = "Ada",
        LastName = "Lovelace"
    };

    [Fact]
    public async Task Register_Then_Login_Succeeds_And_Returns_A_Usable_Token()
    {
        var client = _factory.CreateClient();
        var register = NewRegisterRequest();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", register);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = register.Email,
            Password = register.Password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
        Assert.Equal("User", auth.Role);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Register_With_Duplicate_Email_Returns_Conflict()
    {
        var client = _factory.CreateClient();
        var register = NewRegisterRequest();

        await client.PostAsJsonAsync("/api/auth/register", register);
        var secondAttempt = await client.PostAsJsonAsync("/api/auth/register", register);

        Assert.Equal(HttpStatusCode.Conflict, secondAttempt.StatusCode);
    }

    [Fact]
    public async Task Register_With_Invalid_Payload_Returns_ValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "not-an-email",
            Password = "short",
            FirstName = "",
            LastName = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_Unauthorized()
    {
        var client = _factory.CreateClient();
        var register = NewRegisterRequest();
        await client.PostAsJsonAsync("/api/auth/register", register);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = register.Email,
            Password = "TheWrongPassword!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_Without_Token_Returns_Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
