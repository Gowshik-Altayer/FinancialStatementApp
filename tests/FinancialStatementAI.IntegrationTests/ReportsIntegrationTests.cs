using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

/// <summary>GET /api/reports/{area} — XLSX/PDF export for Statements, Transactions, Review,
/// Reconciliation, and Categories. Each area gets a happy-path (200, correct Content-Type,
/// non-empty body) and an unauthenticated (401) case; the underlying data/filtering is already
/// covered by each area's own list-endpoint tests, so these only exercise the export itself.</summary>
public class ReportsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportsIntegrationTests(CustomWebApplicationFactory factory)
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
            FirstName = "Reba",
            LastName = "Reporter"
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

    private static void AssertIsXlsxDownload(HttpResponseMessage response, byte[] body)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.NotNull(response.Content.Headers.ContentDisposition?.FileName);
        Assert.NotEmpty(body);
    }

    [Fact]
    public async Task Statements_Xlsx_Returns_Ok_With_The_Users_Own_Statements()
    {
        var client = await CreateAuthenticatedClientAsync();
        await UploadAndReprocessAsync(client, "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\nClosing Balance $1100.00");

        var response = await client.GetAsync("/api/reports/statements?format=xlsx");
        var body = await response.Content.ReadAsByteArrayAsync();

        AssertIsXlsxDownload(response, body);
    }

    [Fact]
    public async Task Statements_Report_Without_Authentication_Is_Rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/statements");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Transactions_Xlsx_Returns_Ok_With_The_Users_Own_Transactions()
    {
        var client = await CreateAuthenticatedClientAsync();
        await UploadAndReprocessAsync(client, "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\nClosing Balance $1100.00");

        var response = await client.GetAsync("/api/reports/transactions?format=xlsx");
        var body = await response.Content.ReadAsByteArrayAsync();

        AssertIsXlsxDownload(response, body);
    }

    [Fact]
    public async Task Transactions_Pdf_Returns_Ok_With_Pdf_Content()
    {
        var client = await CreateAuthenticatedClientAsync();
        await UploadAndReprocessAsync(client, "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\nClosing Balance $1100.00");

        var response = await client.GetAsync("/api/reports/transactions?format=pdf");
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(body);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(body, 0, 5));
    }

    [Fact]
    public async Task Transactions_Report_Without_Authentication_Is_Rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/transactions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Review_Xlsx_Returns_Ok_Even_When_The_Queue_Is_Empty()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/reports/review?format=xlsx");
        var body = await response.Content.ReadAsByteArrayAsync();

        AssertIsXlsxDownload(response, body);
    }

    [Fact]
    public async Task Review_Report_Without_Authentication_Is_Rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/review");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reconciliation_Xlsx_Returns_Ok_With_The_Users_Own_Reconciliation_Results()
    {
        var client = await CreateAuthenticatedClientAsync();
        await UploadAndReprocessAsync(client, "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\nClosing Balance $1100.00");

        var response = await client.GetAsync("/api/reports/reconciliation?format=xlsx");
        var body = await response.Content.ReadAsByteArrayAsync();

        AssertIsXlsxDownload(response, body);
    }

    [Fact]
    public async Task Reconciliation_Report_Without_Authentication_Is_Rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/reconciliation");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Categories_Xlsx_Returns_Ok_With_The_Seeded_Category_Taxonomy()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/reports/categories?format=xlsx");
        var body = await response.Content.ReadAsByteArrayAsync();

        AssertIsXlsxDownload(response, body);
    }

    [Fact]
    public async Task Categories_Pdf_Returns_Ok_With_Pdf_Content()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/reports/categories?format=pdf");
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(body);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(body, 0, 5));
    }

    [Fact]
    public async Task Categories_Report_Without_Authentication_Is_Rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reports/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_Unsupported_Format_Value_Is_Rejected_With_BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/reports/categories?format=csv");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
