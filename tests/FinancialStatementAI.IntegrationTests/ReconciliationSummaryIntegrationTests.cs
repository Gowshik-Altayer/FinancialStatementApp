using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

/// <summary>The cross-statement GET /api/reconciliation[/summary] endpoints — as opposed to
/// ReconciliationIntegrationTests, which covers the existing per-statement
/// GET /api/statements/{id}/reconciliation.</summary>
public class ReconciliationSummaryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReconciliationSummaryIntegrationTests(CustomWebApplicationFactory factory)
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
            FirstName = "Rita",
            LastName = "Reconciler"
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
    public async Task Summary_Reports_Reconciled_Mismatch_And_Pending_Across_Different_Statements()
    {
        var client = await CreateAuthenticatedClientAsync();
        await UploadAndReprocessAsync(client,
            "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\n02/08 SOME PAYMENT TRANSACTION MADE TODAY -328.77\nClosing Balance $771.23",
            "reconciled.pdf");
        await UploadAndReprocessAsync(client,
            "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\n02/08 SOME PAYMENT TRANSACTION MADE TODAY -328.77\nClosing Balance $800.00",
            "mismatch.pdf");
        await UploadAndReprocessAsync(client, "01/08 SOME MERCHANT WITH NO BALANCES 45.67", "insufficient.pdf");

        var response = await client.GetAsync("/api/reconciliation/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, summary.GetProperty("reconciledCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("mismatchCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("insufficientInformationCount").GetInt32());
        Assert.True(summary.GetProperty("totalDiscrepancyAmount").GetDecimal() > 0);
    }

    [Fact]
    public async Task PendingCount_Reflects_Statements_Never_Reprocessed_At_All()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildPdfWithText("01/08 SOME MERCHANT 45.67"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "never-reprocessed.pdf");
        await client.PostAsync("/api/statements/upload", content); // uploaded, never reprocessed

        var summary = await (await client.GetAsync("/api/reconciliation/summary")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, summary.GetProperty("pendingCount").GetInt32());
    }

    [Fact]
    public async Task GetAll_Filters_By_Status_And_Excludes_Statements_With_No_Reconciliation_At_All()
    {
        var client = await CreateAuthenticatedClientAsync();
        var reconciledId = await UploadAndReprocessAsync(client,
            "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\n02/08 SOME PAYMENT TRANSACTION MADE TODAY -328.77\nClosing Balance $771.23",
            "reconciled.pdf");
        await UploadAndReprocessAsync(client,
            "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\n02/08 SOME PAYMENT TRANSACTION MADE TODAY -328.77\nClosing Balance $800.00",
            "mismatch.pdf");

        var response = await client.GetAsync("/api/reconciliation?status=Reconciled");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = result.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal(reconciledId, items[0].GetProperty("statementId").GetGuid());
        Assert.Equal("Reconciled", items[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetAll_Never_Returns_Another_Users_Reconciliation_Results()
    {
        var owner = await CreateAuthenticatedClientAsync();
        await UploadAndReprocessAsync(owner,
            "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\n02/08 SOME PAYMENT TRANSACTION MADE TODAY -328.77\nClosing Balance $771.23");

        var intruder = await CreateAuthenticatedClientAsync();
        var response = await intruder.GetAsync("/api/reconciliation");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
    }
}
