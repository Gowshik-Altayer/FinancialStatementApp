using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FinancialStatementAI.Application.DTOs.Auth;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialStatementAI.IntegrationTests;

public class TransactionClassificationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TransactionClassificationIntegrationTests(CustomWebApplicationFactory factory)
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

    private async Task<Guid> UploadAndReprocessAsync(HttpClient client, string pageText)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildPdfWithText(pageText));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");

        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        await client.PostAsync($"/api/statements/{statementId}/reprocess", null);
        return statementId;
    }

    [Fact]
    public async Task A_Known_Merchant_Is_Classified_Via_Merchant_Mapping_With_High_Confidence()
    {
        var client = await CreateAuthenticatedClientAsync();
        var statementId = await UploadAndReprocessAsync(client, "01/08 UBER TRIP RIDESHARE SERVICE PAYMENT 18.20");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transaction = await dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.Classifications)
            .SingleAsync(t => t.StatementId == statementId);

        Assert.Equal("Transportation", transaction.Category!.Name);
        var classification = Assert.Single(transaction.Classifications);
        Assert.Equal(Domain.Enums.ClassificationMethod.MerchantMapping, classification.ClassificationMethod);
        Assert.True(classification.ConfidenceScore >= 0.80m);
    }

    [Fact]
    public async Task A_Structural_Keyword_Is_Classified_Via_Rule_With_High_Confidence()
    {
        var client = await CreateAuthenticatedClientAsync();
        var statementId = await UploadAndReprocessAsync(client, "01/08 PAYROLL DEPOSIT FROM EMPLOYER 2500.00");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transaction = await dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.Classifications)
            .SingleAsync(t => t.StatementId == statementId);

        Assert.Equal("Payroll", transaction.Category!.Name);
        var classification = Assert.Single(transaction.Classifications);
        Assert.Equal(Domain.Enums.ClassificationMethod.Rule, classification.ClassificationMethod);
    }

    [Fact]
    public async Task An_Unknown_Merchant_Falls_Through_To_The_Mock_Llm_With_Low_Confidence()
    {
        var client = await CreateAuthenticatedClientAsync();
        var statementId = await UploadAndReprocessAsync(client, "01/08 ZZYYXX NOVEL MERCHANT CO 40.00");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transaction = await dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.Classifications)
            .SingleAsync(t => t.StatementId == statementId);

        var classification = Assert.Single(transaction.Classifications);
        Assert.Equal(Domain.Enums.ClassificationMethod.Llm, classification.ClassificationMethod);
        Assert.True(classification.ConfidenceScore < 0.60m); // Mock is honest, not falsely confident

        var aiRequest = await dbContext.AIRequests.SingleAsync(r => r.TransactionId == transaction.Id);
        Assert.True(aiRequest.IsSuccess);
    }

    [Fact]
    public async Task Reprocessing_Yields_One_Transaction_With_One_Current_Classification()
    {
        // NOTE: ReplaceForStatementAsync (Phase 9) deletes and recreates a statement's
        // transactions wholesale on reprocess, rather than updating in place — so reprocessing
        // today does not (yet) preserve classification/correction history across the recreated
        // Transaction row. That's a known, documented limitation (see docs/ai-processing.md)
        // that Phase 12 (human review) needs to account for. This test asserts today's actual
        // behavior: exactly one transaction, freshly classified once, after any number of
        // reprocess calls — not that history survives across them.
        var client = await CreateAuthenticatedClientAsync();
        var statementId = await UploadAndReprocessAsync(client, "01/08 UBER TRIP RIDESHARE SERVICE PAYMENT 18.20");

        await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transactions = await dbContext.Transactions
            .Include(t => t.Classifications)
            .Where(t => t.StatementId == statementId)
            .ToListAsync();

        var transaction = Assert.Single(transactions);
        var classification = Assert.Single(transaction.Classifications);
        Assert.True(classification.IsCurrent);
    }
}
