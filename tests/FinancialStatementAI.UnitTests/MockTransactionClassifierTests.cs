using FinancialStatementAI.Infrastructure.AI.Classification;

namespace FinancialStatementAI.UnitTests;

public class MockTransactionClassifierTests
{
    [Fact]
    public async Task ClassifyAsync_Is_Honest_Rather_Than_Falsely_Confident()
    {
        var classifier = new MockTransactionClassifier();

        var result = await classifier.ClassifyAsync("SOME UNKNOWN MERCHANT", 42.00m, ["Groceries", "Other"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("Other", result.CategoryName);
        Assert.True(result.Confidence < 0.60m); // must land in "review required" territory, never falsely confident
    }
}
