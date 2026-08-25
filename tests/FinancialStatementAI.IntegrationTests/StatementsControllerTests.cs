using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

public class StatementsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StatementsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Built the same way as StatementFileValidatorTests' fixture — a minimal PDF with a
    // correctly byte-offset xref table so PdfPig accepts it without needing repair scanning.
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

    private static MultipartFormDataContent BuildUploadContent(byte[] fileBytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Upload_A_Valid_Pdf_Creates_A_Statement_With_Uploaded_Status()
    {
        var client = await CreateAuthenticatedClientAsync();
        var pdfBytes = BuildMinimalPdf();

        var response = await client.PostAsync("/api/statements/upload", BuildUploadContent(pdfBytes, "statement.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Uploaded", json.GetProperty("processingStatus").GetString());
        Assert.Equal("statement.pdf", json.GetProperty("originalFileName").GetString());
    }

    [Fact]
    public async Task Upload_Rejects_An_Unsupported_File_Type()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsync(
            "/api/statements/upload",
            BuildUploadContent([1, 2, 3, 4], "malware.exe", "application/octet-stream"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Without_A_Token_Is_Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/statements/upload",
            BuildUploadContent(BuildMinimalPdf(), "statement.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Uploaded_Statement_Appears_In_The_Users_List_And_Can_Be_Fetched_By_Id()
    {
        var client = await CreateAuthenticatedClientAsync();
        var uploadResponse = await client.PostAsync(
            "/api/statements/upload",
            BuildUploadContent(BuildMinimalPdf(), "statement.pdf", "application/pdf"));
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        var listResponse = await client.GetAsync("/api/statements");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(list.GetProperty("items").EnumerateArray(), s => s.GetProperty("id").GetGuid() == statementId);

        var detailResponse = await client.GetAsync($"/api/statements/{statementId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        var statusResponse = await client.GetAsync($"/api/statements/{statementId}/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
    }

    [Fact]
    public async Task Another_Users_Statement_Is_Not_Accessible()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var uploadResponse = await owner.PostAsync(
            "/api/statements/upload",
            BuildUploadContent(BuildMinimalPdf(), "statement.pdf", "application/pdf"));
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        var intruder = await CreateAuthenticatedClientAsync();
        var response = await intruder.GetAsync($"/api/statements/{statementId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
