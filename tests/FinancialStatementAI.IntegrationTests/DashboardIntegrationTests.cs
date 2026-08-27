using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

public class DashboardIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DashboardIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static byte[] BuildMinimalPdf()
    {
        string[] objects =
        [
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>\nendobj\n"
        ];

        var body = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(body.ToString()));
            body.Append(obj);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(body.ToString());
        body.Append("xref\n");
        body.Append($"0 {objects.Length + 1}\n");
        body.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            body.Append($"{offset:D10} 00000 n \n");
        }
        body.Append("trailer\n");
        body.Append($"<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        body.Append("startxref\n");
        body.Append($"{xrefOffset}\n");
        body.Append("%%EOF");

        return Encoding.ASCII.GetBytes(body.ToString());
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"{Guid.NewGuid():N}@example.com",
            Password = "Sup3rSecret!",
            FirstName = "Dana",
            LastName = "Dashboard"
        });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Fact]
    public async Task Summary_Returns_Zeroed_Kpis_For_A_Brand_New_User_With_No_Statements()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("kpis").GetProperty("totalStatements").GetInt32());
        // Never fabricated as 0 when there's genuinely nothing to average.
        Assert.True(body.GetProperty("kpis").GetProperty("averageClassificationConfidence").ValueKind == JsonValueKind.Null);
        Assert.Equal(8, body.GetProperty("pipelineStages").GetArrayLength());
    }

    [Fact]
    public async Task A_New_Users_Data_Never_Leaks_Into_Another_Users_Summary()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildMinimalPdf());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");
        var uploadResponse = await owner.PostAsync("/api/statements/upload", content);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var ownerSummary = await (await owner.GetAsync("/api/dashboard/summary")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, ownerSummary.GetProperty("kpis").GetProperty("totalStatements").GetInt32());

        var otherUser = await CreateAuthenticatedClientAsync();
        var otherSummary = await (await otherUser.GetAsync("/api/dashboard/summary")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, otherSummary.GetProperty("kpis").GetProperty("totalStatements").GetInt32());
    }

    [Fact]
    public async Task Config_Returns_The_Full_Widget_Registry_Resolved_For_The_Current_Users_Role()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/dashboard/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var widgets = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(widgets.GetArrayLength() > 0);
        Assert.All(widgets.EnumerateArray(), w => Assert.False(string.IsNullOrEmpty(w.GetProperty("widgetKey").GetString())));
    }

    [Fact]
    public async Task Updating_My_Config_Persists_And_Is_Reflected_On_The_Next_Fetch()
    {
        var client = await CreateAuthenticatedClientAsync();
        var current = await (await client.GetAsync("/api/dashboard/config")).Content.ReadFromJsonAsync<JsonElement>();
        var firstWidgetKey = current[0].GetProperty("widgetKey").GetString();

        var updateResponse = await client.PutAsJsonAsync("/api/dashboard/config", new
        {
            Items = new[] { new { WidgetKey = firstWidgetKey, IsVisible = false, SortOrder = 99 } }
        });
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var refreshed = await (await client.GetAsync("/api/dashboard/config")).Content.ReadFromJsonAsync<JsonElement>();
        var updatedWidget = refreshed.EnumerateArray().Single(w => w.GetProperty("widgetKey").GetString() == firstWidgetKey);
        Assert.False(updatedWidget.GetProperty("isVisible").GetBoolean());
        Assert.Equal("UserOverride", updatedWidget.GetProperty("source").GetString());
    }

    [Fact]
    public async Task A_Non_Admin_Cannot_Change_Role_Defaults()
    {
        var client = await CreateAuthenticatedClientAsync(); // registers as the default "User" role

        var response = await client.PutAsJsonAsync("/api/dashboard/config/role-defaults/User", new { Items = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
