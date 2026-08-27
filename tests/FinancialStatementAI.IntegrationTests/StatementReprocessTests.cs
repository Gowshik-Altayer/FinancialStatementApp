using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

public class StatementReprocessTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StatementReprocessTests(CustomWebApplicationFactory factory)
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

    // Unlike BuildPdfWithText (one Tj call, one visual line), this places each line at its own Td
    // position — the realistic shape of a multi-line statement, and the specific shape that once
    // silently defeated transaction parsing: PdfPig's raw Page.Text concatenates text runs with no
    // line-break awareness, so TransactionExtractionService (which requires one transaction per
    // line) found zero transactions even though HasUsableText was true. Fixed by switching
    // PdfTextExtractionService to ContentOrderTextExtractor — this test guards the regression.
    private static byte[] BuildPdfWithMultipleTextLines(IReadOnlyList<string> lines)
    {
        var streamParts = new List<string> { "BT", "/F1 10 Tf", "72 740 Td" };
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                streamParts.Add("0 -14 Td");
            }
            streamParts.Add($"({lines[i]}) Tj");
        }
        streamParts.Add("ET");
        var contentStream = string.Join('\n', streamParts);
        var contentBytes = Encoding.ASCII.GetByteCount(contentStream);

        string[] objects =
        [
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>\nendobj\n",
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

    // Same shape as StatementFileValidatorTests'/PdfTextExtractionServiceTests' "no content
    // stream" fixture: a structurally valid PDF whose page has nothing for PdfPig to extract,
    // simulating a scanned page with no embedded text layer.
    private static byte[] BuildPdfWithNoContentStream()
    {
        string[] objects =
        [
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n"
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

    [Fact]
    public async Task Reprocess_A_Pdf_With_Usable_Text_Marks_Extraction_Complete()
    {
        var client = await CreateAuthenticatedClientAsync();
        var pdfBytes = BuildPdfWithText(
            "01/08 AMAZON WEB SERVICES 129.45 02/08 UBER TRIP 18.20 03/08 WHOLE FOODS MARKET 64.02 payment thank you");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");

        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        var reprocessResponse = await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        Assert.Equal(HttpStatusCode.OK, reprocessResponse.StatusCode);
        var result = await reprocessResponse.Content.ReadFromJsonAsync<JsonElement>();
        // As of Phase 11, a successful synchronous reprocess runs all the way through
        // classification and reconciliation, ending at PendingReview for a human to check.
        Assert.Equal("PendingReview", result.GetProperty("processingStatus").GetString());
        Assert.True(result.GetProperty("hasUsableText").GetBoolean());
        Assert.Equal(1, result.GetProperty("extractedPageCount").GetInt32());
    }

    [Fact]
    public async Task Reprocess_A_Multi_Line_Pdf_Extracts_Every_Transaction()
    {
        var client = await CreateAuthenticatedClientAsync();
        var pdfBytes = BuildPdfWithMultipleTextLines([
            "FIRST CAPITAL BANK",
            "Account Holder: Ada Lovelace",
            "07/02     AMAZON WEB SERVICES                  -129.45",
            "07/03     UBER TRIP                              -18.20",
            "07/05     WHOLE FOODS MARKET                     -64.02"
        ]);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "multi-line-statement.pdf");

        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        var reprocessResponse = await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        Assert.Equal(HttpStatusCode.OK, reprocessResponse.StatusCode);
        var result = await reprocessResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DirectPdfText", result.GetProperty("extractionMethod").GetString());
        Assert.Equal(3, result.GetProperty("transactionCount").GetInt32());
    }

    [Fact]
    public async Task Reprocess_A_Pdf_With_No_Usable_Text_Falls_Back_To_Ocr()
    {
        var client = await CreateAuthenticatedClientAsync();
        var pdfBytes = BuildPdfWithNoContentStream();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "scanned-statement.pdf");

        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        var reprocessResponse = await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        Assert.Equal(HttpStatusCode.OK, reprocessResponse.StatusCode);
        var result = await reprocessResponse.Content.ReadFromJsonAsync<JsonElement>();
        // MockOcrService (the default provider) always succeeds with usable simulated text, so
        // a PDF with no direct text layer should still reach PendingReview via OCR.
        Assert.Equal("PendingReview", result.GetProperty("processingStatus").GetString());
        Assert.Equal("Ocr", result.GetProperty("extractionMethod").GetString());
    }

    [Fact]
    public async Task Reprocess_For_Another_Users_Statement_Returns_NotFound()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildPdfWithText("some statement text"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");
        var uploadResponse = await owner.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        var intruder = await CreateAuthenticatedClientAsync();
        var response = await intruder.PostAsync($"/api/statements/{statementId}/reprocess", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
