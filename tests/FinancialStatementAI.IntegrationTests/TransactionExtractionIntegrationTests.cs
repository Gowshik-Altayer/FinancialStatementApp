using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialStatementAI.IntegrationTests;

public class TransactionExtractionIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TransactionExtractionIntegrationTests(CustomWebApplicationFactory factory)
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

    private async Task<Guid> UploadAndReprocessAsync(HttpClient client, string pageText, string fileName = "statement.pdf")
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
    public async Task Reprocessing_A_Statement_Persists_Normalized_Transactions()
    {
        var client = await CreateAuthenticatedClientAsync();
        var statementId = await UploadAndReprocessAsync(
            client,
            "01/08 AMAZON WEB SERVICES 129.45");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transactions = await dbContext.Transactions
            .Where(t => t.StatementId == statementId)
            .ToListAsync();

        var transaction = Assert.Single(transactions);
        Assert.Equal("AMAZON WEB SERVICES", transaction.Description);
        Assert.Equal(129.45m, transaction.Amount);
    }

    [Fact]
    public async Task Reprocessing_Replaces_The_Statements_Own_Prior_Parse_Instead_Of_Duplicating()
    {
        var client = await CreateAuthenticatedClientAsync();
        var statementId = await UploadAndReprocessAsync(client, "01/08 AMAZON WEB SERVICES 129.45");

        // Reprocess again — should replace, not add to, the previously parsed transaction.
        await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await dbContext.Transactions.CountAsync(t => t.StatementId == statementId);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task A_Transaction_Matching_One_From_An_Earlier_Statement_Is_Flagged_As_A_Potential_Duplicate()
    {
        var client = await CreateAuthenticatedClientAsync();
        const string sameLine = "02/10 DUPLICATE MERCHANT CHECK 75.00";

        var firstStatementId = await UploadAndReprocessAsync(client, sameLine, "first.pdf");
        var secondStatementId = await UploadAndReprocessAsync(client, sameLine, "second.pdf");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var firstTransaction = await dbContext.Transactions.SingleAsync(t => t.StatementId == firstStatementId);
        var secondTransaction = await dbContext.Transactions.SingleAsync(t => t.StatementId == secondStatementId);

        Assert.False(firstTransaction.IsPotentialDuplicate);
        Assert.True(secondTransaction.IsPotentialDuplicate);
        Assert.Equal(firstTransaction.Id, secondTransaction.DuplicateOfTransactionId);
    }
}
