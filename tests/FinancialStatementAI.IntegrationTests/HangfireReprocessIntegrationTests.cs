using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Auth;
using FinancialStatementAI.Application.Interfaces;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialStatementAI.IntegrationTests;

/// <summary>Verifies the real "BackgroundJobs:Provider" = "Hangfire" path (Phase 14) — as opposed
/// to every other test in this project, which exercises the default synchronous scheduler.
/// Deliberately does not start a Hangfire server, so these assertions are against the enqueued
/// job itself rather than its (asynchronous, timing-dependent) eventual completion.</summary>
public class HangfireReprocessIntegrationTests : IClassFixture<HangfireWebApplicationFactory>
{
    private readonly HangfireWebApplicationFactory _factory;

    public HangfireReprocessIntegrationTests(HangfireWebApplicationFactory factory)
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

    [Fact]
    public async Task Reprocess_Returns_Accepted_And_Flips_The_Statement_To_Processing()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildMinimalPdf());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");
        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        var reprocessResponse = await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        Assert.Equal(HttpStatusCode.Accepted, reprocessResponse.StatusCode);
        var result = await reprocessResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Processing", result.GetProperty("processingStatus").GetString());
    }

    [Fact]
    public async Task Reprocess_Actually_Enqueues_A_Hangfire_Job_For_The_Pipeline()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildMinimalPdf());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");
        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        using var scope = _factory.Services.CreateScope();
        var jobStorage = scope.ServiceProvider.GetRequiredService<JobStorage>();
        var monitoringApi = jobStorage.GetMonitoringApi();
        var enqueuedJobs = monitoringApi.EnqueuedJobs("default", 0, 50);

        Assert.Contains(enqueuedJobs, j =>
            j.Value.Job.Type == typeof(IStatementProcessingService) &&
            j.Value.Job.Method.Name == nameof(IStatementProcessingService.ProcessAsync) &&
            (Guid)j.Value.Job.Args[0] == statementId);
    }

    [Fact]
    public async Task Reprocess_Records_A_Pending_ProcessingJob_Row_With_A_Hangfire_Job_Id()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(BuildMinimalPdf());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "statement.pdf");
        var uploadResponse = await client.PostAsync("/api/statements/upload", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var statementId = uploaded.GetProperty("id").GetGuid();

        await client.PostAsync($"/api/statements/{statementId}/reprocess", null);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinancialStatementAI.Infrastructure.Persistence.AppDbContext>();
        var jobs = dbContext.ProcessingJobs.Where(j => j.StatementId == statementId).ToList();

        // Upload itself already creates one Pending ProcessingJob (Stage Upload, Phase 6); the
        // reprocess request creates a second one — this assertion is about that second row.
        var reprocessJob = Assert.Single(jobs, j => j.HangfireJobId != null);
        Assert.Equal(FinancialStatementAI.Domain.Enums.ProcessingJobStatus.Pending, reprocessJob.Status);
    }
}
