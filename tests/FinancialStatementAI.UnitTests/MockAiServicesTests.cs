using FinancialStatementAI.Infrastructure.AI.DocumentIntelligence;

namespace FinancialStatementAI.UnitTests;

public class MockAiServicesTests
{
    [Fact]
    public async Task MockDocumentIntelligenceService_Returns_A_Successful_Result()
    {
        var service = new MockDocumentIntelligenceService();

        var result = await service.AnalyzeAsync(new MemoryStream(), "application/pdf");

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Fields);
    }
}
