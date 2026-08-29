using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;

namespace FinancialStatementAI.IntegrationTests;

public class TransactionReviewIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TransactionReviewIntegrationTests(CustomWebApplicationFactory factory)
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

    private async Task<(Guid StatementId, Guid TransactionId)> UploadReprocessAndGetSoleTransactionAsync(HttpClient client, string pageText)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildPdfWithText(pageText));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");

        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        var transactionsResponse = await client.GetAsync($"/api/statements/{statementId}/transactions");
        var transactions = await transactionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = transactions.EnumerateArray().Single().GetProperty("id").GetGuid();

        return (statementId, transactionId);
    }

    [Fact]
    public async Task Correcting_A_Transactions_Category_Records_The_Original_And_New_Value()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (_, transactionId) = await UploadReprocessAndGetSoleTransactionAsync(client, "01/08 ZZYYXX NOVEL MERCHANT CO 40.00");

        var response = await client.PostAsJsonAsync($"/api/transactions/{transactionId}/corrections", new { categoryName = "Groceries", reason = "Manual review" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Groceries", updated.GetProperty("categoryName").GetString());
        Assert.True(updated.GetProperty("hasBeenCorrected").GetBoolean());

        var corrections = updated.GetProperty("corrections").EnumerateArray().ToList();
        var correction = Assert.Single(corrections);
        Assert.Equal("Category", correction.GetProperty("fieldName").GetString());
        Assert.Equal("Other", correction.GetProperty("originalValue").GetString()); // MockTransactionClassifier's honest default
        Assert.Equal("Groceries", correction.GetProperty("correctedValue").GetString());
        Assert.Equal("Manual review", correction.GetProperty("correctionReason").GetString());
    }

    [Fact]
    public async Task Correcting_Several_Fields_At_Once_Persists_All_Of_Them_With_Separate_Audit_Rows()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (_, transactionId) = await UploadReprocessAndGetSoleTransactionAsync(client, "01/08 ZZYYXX ORIGINAL MERCHANT 40.00");

        var response = await client.PostAsJsonAsync($"/api/transactions/{transactionId}/corrections", new
        {
            transactionDate = "2026-02-01",
            description = "Corrected description",
            merchant = "Corrected Merchant",
            amount = -55.25,
            transactionType = "Refund"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-02-01", updated.GetProperty("transactionDate").GetString());
        Assert.Equal("Corrected description", updated.GetProperty("description").GetString());
        Assert.Equal("Corrected Merchant", updated.GetProperty("merchant").GetString());
        Assert.Equal(-55.25, updated.GetProperty("amount").GetDouble());
        Assert.Equal("Refund", updated.GetProperty("transactionType").GetString());

        var fieldNames = updated.GetProperty("corrections").EnumerateArray().Select(c => c.GetProperty("fieldName").GetString()).ToList();
        Assert.Equal(5, fieldNames.Count); // one audit row per corrected field
        Assert.Contains("TransactionDate", fieldNames);
        Assert.Contains("Description", fieldNames);
        Assert.Contains("Merchant", fieldNames);
        Assert.Contains("Amount", fieldNames);
        Assert.Contains("TransactionType", fieldNames);
    }

    [Fact]
    public async Task Correcting_A_Transactions_Amount_Re_Reconciles_The_Statement()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (statementId, transactionId) = await UploadReprocessAndGetSoleTransactionAsync(
            client, "Opening Balance $1000.00\n01/08 GROCERY STORE WEEKLY SHOPPING TRIP 100.00\nClosing Balance $1100.00");

        var beforeCorrection = await client.GetAsync($"/api/statements/{statementId}/reconciliation");
        var reconciliationBefore = await beforeCorrection.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Reconciled", reconciliationBefore.GetProperty("status").GetString()); // 1000 + 100 - 0 = 1100

        var correctionResponse = await client.PostAsJsonAsync($"/api/transactions/{transactionId}/corrections", new { amount = -50.00 });
        Assert.Equal(HttpStatusCode.OK, correctionResponse.StatusCode);

        var afterCorrection = await client.GetAsync($"/api/statements/{statementId}/reconciliation");
        var reconciliationAfter = await afterCorrection.Content.ReadFromJsonAsync<JsonElement>();
        // 1000 + 0 credits - 50 debits = 950, but the statement still reports a $1100 closing
        // balance — the correction must have re-triggered reconciliation against the live totals.
        Assert.Equal("Mismatch", reconciliationAfter.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Correcting_To_An_Unknown_Category_Returns_BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (_, transactionId) = await UploadReprocessAndGetSoleTransactionAsync(client, "01/08 SOME MERCHANT 40.00");

        var response = await client.PostAsJsonAsync($"/api/transactions/{transactionId}/corrections", new { categoryName = "Not A Real Category" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Correcting_Another_Users_Transaction_Returns_NotFound()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var (_, transactionId) = await UploadReprocessAndGetSoleTransactionAsync(owner, "01/08 SOME MERCHANT 40.00");

        var intruder = await CreateAuthenticatedClientAsync();
        var response = await intruder.PostAsJsonAsync($"/api/transactions/{transactionId}/corrections", new { categoryName = "Groceries" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_Category_Correction_Survives_Reprocessing_The_Same_Statement_Again()
    {
        // This is the core Phase 12 fix: TransactionRepository.ReplaceForStatementAsync now
        // matches the reparsed line back to the same Transaction row by natural key instead of
        // deleting and recreating it, so this correction (and the row it lives on) survives.
        // Reclassification then finds the correction via the "Known Classification" rung
        // (matching on merchant text) and re-applies it — see docs/ai-processing.md.
        var client = await CreateAuthenticatedClientAsync();
        var (statementId, transactionId) = await UploadReprocessAndGetSoleTransactionAsync(client, "01/08 ZZYYXX NOVEL MERCHANT CO 40.00");

        var correctResponse = await client.PostAsJsonAsync($"/api/transactions/{transactionId}/corrections", new { categoryName = "Groceries" });
        Assert.Equal(HttpStatusCode.OK, correctResponse.StatusCode);

        var reprocessResponse = await client.PostAsync($"/api/statements/{statementId}/reprocess", null);
        Assert.Equal(HttpStatusCode.OK, reprocessResponse.StatusCode);

        var transactionsResponse = await client.GetAsync($"/api/statements/{statementId}/transactions");
        var transactions = await transactionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var transaction = transactions.EnumerateArray().Single();

        Assert.Equal(transactionId, transaction.GetProperty("id").GetGuid()); // same row, not a new one
        Assert.Equal("Groceries", transaction.GetProperty("categoryName").GetString());
        Assert.Equal("PreviousCorrection", transaction.GetProperty("classificationMethod").GetString());
    }

    [Fact]
    public async Task Review_Queue_Only_Surfaces_Transactions_From_PendingReview_Statements()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (statementId, transactionId) = await UploadReprocessAndGetSoleTransactionAsync(client, "01/08 SOME MERCHANT 40.00");

        var beforeVerify = await client.GetAsync("/api/transactions/review-queue");
        var queueBefore = await beforeVerify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(queueBefore.EnumerateArray(), t => t.GetProperty("id").GetGuid() == transactionId);

        var verifyResponse = await client.PostAsync($"/api/statements/{statementId}/verify", null);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var afterVerify = await client.GetAsync("/api/transactions/review-queue");
        var queueAfter = await afterVerify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(queueAfter.EnumerateArray(), t => t.GetProperty("id").GetGuid() == transactionId);
    }

    [Fact]
    public async Task Verifying_A_Statement_Not_Yet_PendingReview_Returns_BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildPdfWithText("01/08 SOME MERCHANT 40.00"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");
        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        // No reprocess call — statement is still just Uploaded, not PendingReview.
        var response = await client.PostAsync($"/api/statements/{statementId}/verify", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Bulk corrections: "apply to all transactions from this merchant" -----------------------

    [Fact]
    public async Task Bulk_Correcting_A_Category_Updates_Every_Transaction_With_The_Same_Merchant()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (_, firstTransactionId) = await UploadReprocessAndGetSoleTransactionAsync(client, "01/08 ZZYYXX SAME MERCHANT CO 40.00");
        var (_, secondTransactionId) = await UploadReprocessAndGetSoleTransactionAsync(client, "01/09 ZZYYXX SAME MERCHANT CO 55.00");

        var response = await client.PostAsJsonAsync($"/api/transactions/{firstTransactionId}/corrections/bulk", new { categoryName = "Groceries" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("updatedCount").GetInt32());
        Assert.Equal("Groceries", body.GetProperty("transaction").GetProperty("categoryName").GetString());

        var secondTransactionResponse = await client.GetAsync($"/api/transactions?search={Uri.EscapeDataString("SAME MERCHANT")}");
        var secondTransaction = (await secondTransactionResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == secondTransactionId);
        Assert.Equal("Groceries", secondTransaction.GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task Bulk_Correcting_To_An_Unknown_Category_Returns_BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (_, transactionId) = await UploadReprocessAndGetSoleTransactionAsync(client, "01/08 SOME MERCHANT 40.00");

        var response = await client.PostAsJsonAsync($"/api/transactions/{transactionId}/corrections/bulk", new { categoryName = "Not A Real Category" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_Correcting_Another_Users_Transaction_Returns_NotFound()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var (_, transactionId) = await UploadReprocessAndGetSoleTransactionAsync(owner, "01/08 SOME MERCHANT 40.00");

        var intruder = await CreateAuthenticatedClientAsync();
        var response = await intruder.PostAsJsonAsync($"/api/transactions/{transactionId}/corrections/bulk", new { categoryName = "Groceries" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Verifying_Twice_The_Second_Time_Returns_BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var (statementId, _) = await UploadReprocessAndGetSoleTransactionAsync(client, "01/08 SOME MERCHANT 40.00");

        var first = await client.PostAsync($"/api/statements/{statementId}/verify", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsync($"/api/statements/{statementId}/verify", null);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }
}
