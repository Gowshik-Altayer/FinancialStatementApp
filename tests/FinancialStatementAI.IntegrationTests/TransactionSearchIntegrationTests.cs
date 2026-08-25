using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

public class TransactionSearchIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TransactionSearchIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static byte[] BuildPdfWithText(string pageText)
    {
        var contentStream = $"BT /F1 12 Tf 72 700 Td ({pageText}) Tj ET";
        var contentBytes = Encoding.ASCII.GetByteCount(contentStream);

        string[] objects =
        [
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            $"5 0 obj\n<< /Length {contentBytes} >>\nstream\n{contentStream}\nendstream\nendobj\n"
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
            FirstName = "Ada",
            LastName = "Lovelace"
        });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private static async Task<Guid> UploadAndReprocessAsync(HttpClient client, string pageText, string fileName = "statement.pdf")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildPdfWithText(pageText));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);

        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        await client.PostAsync($"/api/statements/{statementId}/reprocess", null);
        return statementId;
    }

    [Fact]
    public async Task Search_By_Description_Substring_Is_Case_Insensitive()
    {
        var client = await CreateAuthenticatedClientAsync();
        await UploadAndReprocessAsync(client, "01/08 WHOLE FOODS MARKET 64.02");
        await UploadAndReprocessAsync(client, "01/08 UBER TRIP RIDESHARE 18.20", "second.pdf");

        var response = await client.GetAsync("/api/transactions?search=whole foods");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = result.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Contains("WHOLE FOODS", items[0].GetProperty("description").GetString());
    }

    [Fact]
    public async Task Filtering_By_StatementId_Only_Returns_That_Statements_Transactions()
    {
        var client = await CreateAuthenticatedClientAsync();
        var firstStatementId = await UploadAndReprocessAsync(client, "01/08 WHOLE FOODS MARKET 64.02", "first.pdf");
        await UploadAndReprocessAsync(client, "01/08 UBER TRIP RIDESHARE 18.20", "second.pdf");

        var response = await client.GetAsync($"/api/transactions?statementId={firstStatementId}");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = result.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal(firstStatementId, items[0].GetProperty("statementId").GetGuid());
    }

    [Fact]
    public async Task Filtering_By_CategoryId_After_A_Correction_Only_Returns_Matching_Transactions()
    {
        var client = await CreateAuthenticatedClientAsync();
        await UploadAndReprocessAsync(client, "01/08 WHOLE FOODS MARKET 64.02", "first.pdf");
        await UploadAndReprocessAsync(client, "01/08 UBER TRIP RIDESHARE 18.20", "second.pdf");

        var allResponse = await client.GetAsync("/api/transactions");
        var all = await allResponse.Content.ReadFromJsonAsync<JsonElement>();
        var groceryTransaction = all.GetProperty("items").EnumerateArray().Single(t => t.GetProperty("description").GetString()!.Contains("WHOLE FOODS"));
        var transactionId = groceryTransaction.GetProperty("id").GetGuid();

        var correctResponse = await client.PostAsJsonAsync($"/api/transactions/{transactionId}/corrections", new { categoryName = "Groceries" });
        var corrected = await correctResponse.Content.ReadFromJsonAsync<JsonElement>();
        var groceriesCategoryId = corrected.GetProperty("categoryId").GetGuid();

        var filteredResponse = await client.GetAsync($"/api/transactions?categoryId={groceriesCategoryId}");
        var filtered = await filteredResponse.Content.ReadFromJsonAsync<JsonElement>();
        var filteredItems = filtered.GetProperty("items").EnumerateArray().ToList();

        Assert.Single(filteredItems);
        Assert.Equal(transactionId, filteredItems[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Search_Never_Returns_Another_Users_Transactions()
    {
        var owner = await CreateAuthenticatedClientAsync();
        await UploadAndReprocessAsync(owner, "01/08 WHOLE FOODS MARKET 64.02");

        var intruder = await CreateAuthenticatedClientAsync();
        var response = await intruder.GetAsync("/api/transactions");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
    }
}
