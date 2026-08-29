using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

public class StatementSearchIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StatementSearchIntegrationTests(CustomWebApplicationFactory factory)
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
            FirstName = "Ada",
            LastName = "Lovelace"
        });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private static async Task<Guid> UploadAsync(HttpClient client, string fileName)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildMinimalPdf());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);

        var response = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await response.Content.ReadFromJsonAsync<JsonElement>();
        return uploaded.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Search_By_Filename_Substring_Is_Case_Insensitive_And_Excludes_Non_Matches()
    {
        var client = await CreateAuthenticatedClientAsync();
        await UploadAsync(client, "chase-checking-january.pdf");
        await UploadAsync(client, "amex-statement.pdf");

        var response = await client.GetAsync("/api/statements?search=CHASE");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = result.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("chase-checking-january.pdf", items[0].GetProperty("originalFileName").GetString());
        Assert.Equal(1, result.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Status_Filter_Only_Returns_Statements_In_That_Status()
    {
        var client = await CreateAuthenticatedClientAsync();
        var uploadedOnlyId = await UploadAsync(client, "still-uploaded.pdf");
        var reprocessedId = await UploadAsync(client, "reprocessed.pdf");
        await client.PostAsync($"/api/statements/{reprocessedId}/reprocess", null);

        var uploadedResponse = await client.GetAsync("/api/statements?status=Uploaded");
        var uploadedResult = await uploadedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var uploadedIds = uploadedResult.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(uploadedOnlyId, uploadedIds);
        Assert.DoesNotContain(reprocessedId, uploadedIds);

        var reviewResponse = await client.GetAsync("/api/statements?status=PendingReview");
        var reviewResult = await reviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var reviewIds = reviewResult.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(reprocessedId, reviewIds);
        Assert.DoesNotContain(uploadedOnlyId, reviewIds);
    }

    [Fact]
    public async Task Pagination_Splits_Results_Across_Pages_Without_Duplicates_Or_Gaps()
    {
        var client = await CreateAuthenticatedClientAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add(await UploadAsync(client, $"statement-{i}.pdf"));
        }

        var firstPageResponse = await client.GetAsync("/api/statements?pageSize=2&page=1");
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, firstPage.GetProperty("totalCount").GetInt32());
        var firstPageIds = firstPage.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(2, firstPageIds.Count);

        var secondPageResponse = await client.GetAsync("/api/statements?pageSize=2&page=2");
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secondPageIds = secondPage.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        Assert.Single(secondPageIds);

        var allReturnedIds = firstPageIds.Concat(secondPageIds).ToList();
        Assert.Equal(ids.OrderBy(x => x), allReturnedIds.OrderBy(x => x));
    }

    [Fact]
    public async Task An_Oversized_PageSize_Is_Clamped_Rather_Than_Honored_Verbatim()
    {
        var client = await CreateAuthenticatedClientAsync();
        await UploadAsync(client, "one.pdf");

        var response = await client.GetAsync("/api/statements?pageSize=99999");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("pageSize").GetInt32() <= 100);
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

    [Fact]
    public async Task The_List_Includes_Account_And_Statement_Period_After_Processing()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildPdfWithText(
            "Account Holder Name: Ada Lovelace\nStatement Period: 03/01/2026 - 03/31/2026\n01/08 WHOLE FOODS MARKET 64.02"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");
        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();
        await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        var response = await client.GetAsync("/api/statements");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = result.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("Ada Lovelace", item.GetProperty("accountHolderName").GetString());
        Assert.Equal("2026-03-01", item.GetProperty("statementPeriodStart").GetString());
        Assert.Equal("2026-03-31", item.GetProperty("statementPeriodEnd").GetString());
    }
}
