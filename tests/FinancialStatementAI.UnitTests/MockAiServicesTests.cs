using FinancialStatementAI.Infrastructure.AI.DocumentIntelligence;
using FinancialStatementAI.Infrastructure.OCR;

namespace FinancialStatementAI.UnitTests;

public class MockAiServicesTests
{
    [Fact]
    public async Task MockOcrService_Returns_Usable_Simulated_Text()
    {
        var service = new MockOcrService();

        var result = await service.ExtractTextAsync(new MemoryStream(), "image/png");

        Assert.True(result.IsSuccess);
        Assert.Contains("MOCK OCR OUTPUT", result.RawText);
        Assert.True(result.RawText.Count(c => !char.IsWhiteSpace(c)) > 20);
    }

    [Fact]
    public async Task MockDocumentIntelligenceService_Returns_A_Successful_Result()
    {
        var service = new MockDocumentIntelligenceService();

        var result = await service.AnalyzeAsync(new MemoryStream(), "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Fields);
    }
}
