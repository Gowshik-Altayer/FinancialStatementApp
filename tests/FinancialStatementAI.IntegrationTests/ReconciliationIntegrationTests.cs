using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

public class ReconciliationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReconciliationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static byte[] BuildPdfWithText(string pageText)
    {
        // A raw (unescaped) newline byte inside a PDF string literal is preserved by the PDF
        // spec as a literal LF character in the string's content, and PdfPig's Page.Text
        // reproduces it faithfully — verified empirically. This lets a single Tj call carry
        // multiple statement lines while still round-tripping as line-separated text.
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

    private async Task<Guid> UploadAndReprocessAsync(HttpClient client, string pageText)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildPdfWithText(pageText));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");

        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        await client.PostAsync($"/api/statements/{statementId}/reprocess", null);
        return statementId;
    }

    [Fact]
    public async Task GetReconciliation_Returns_InsufficientInformation_When_Statement_Has_No_Balances()
    {
        // Our label-driven field extractor won't find "Opening Balance"/"Closing Balance" labels
        // in this plain transaction-only text, so both stay null on the Statement.
        var client = await CreateAuthenticatedClientAsync();
        var statementId = await UploadAndReprocessAsync(client, "01/08 SOME MERCHANT WITH A LONG NAME 45.67");

        var response = await client.GetAsync($"/api/statements/{statementId}/reconciliation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InsufficientInformation", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetReconciliation_Reports_Reconciled_When_Balances_Match_The_Transactions()
    {
        var client = await CreateAuthenticatedClientAsync();
        var statementId = await UploadAndReprocessAsync(
            client,
            "Opening Balance $1000.00\n" +
            "01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\n" +
            "02/08 SOME PAYMENT TRANSACTION MADE TODAY -328.77\n" +
            "Closing Balance $771.23");

        var response = await client.GetAsync($"/api/statements/{statementId}/reconciliation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Reconciled", result.GetProperty("status").GetString());
        Assert.Equal(771.23, result.GetProperty("expectedClosingBalance").GetDouble());
    }

    [Fact]
    public async Task GetReconciliation_Before_Any_Reprocess_Returns_NotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildPdfWithText("01/08 SOME MERCHANT WITH A LONG NAME 45.67"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");
        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        // No reprocess call — reconciliation has never run for this statement.
        var response = await client.GetAsync($"/api/statements/{statementId}/reconciliation");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_Statement_List_And_Detail_Both_Surface_The_Latest_Reconciliation_Status()
    {
        var client = await CreateAuthenticatedClientAsync();
        var statementId = await UploadAndReprocessAsync(
            client,
            "Opening Balance $1000.00\n" +
            "01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\n" +
            "Closing Balance $771.23"); // deliberately wrong on purpose, to get a Mismatch

        var listResponse = await client.GetAsync("/api/statements");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var listedStatement = list.GetProperty("items").EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == statementId);
        Assert.Equal("Mismatch", listedStatement.GetProperty("reconciliationStatus").GetString());

        var detailResponse = await client.GetAsync($"/api/statements/{statementId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Mismatch", detail.GetProperty("reconciliationStatus").GetString());
    }
}
