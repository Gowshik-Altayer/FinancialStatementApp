using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class ReconciliationServiceTests
{
    private readonly Mock<IStatementRepository> _statementRepository = new();
    private readonly Mock<IReconciliationRepository> _reconciliationRepository = new();

    private ReconciliationService CreateService() => new(_statementRepository.Object, _reconciliationRepository.Object);

    private static Transaction Tx(decimal amount) => new() { Amount = amount };

    private void SetUpStatement(decimal? opening, decimal? closing, params decimal[] transactionAmounts)
    {
        var statement = new Statement
        {
            OpeningBalance = opening,
            ClosingBalance = closing,
            Transactions = transactionAmounts.Select(Tx).ToList()
        };
        _statementRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(statement);
    }

    [Fact]
    public async Task Balances_That_Match_Exactly_Are_Reconciled()
    {
        // Opening 1000 + Credits 100 - Debits 328.77 = 771.23, matching the statement's own closing balance.
        SetUpStatement(1000.00m, 771.23m, 100.00m, -328.77m);

        var result = await CreateService().ReconcileAsync(Guid.NewGuid());

        Assert.Equal("Reconciled", result.Status);
        Assert.Equal(771.23m, result.ExpectedClosingBalance);
        Assert.Equal(0m, result.Discrepancy);
    }

    [Fact]
    public async Task A_Real_Discrepancy_Is_Reported_As_A_Mismatch()
    {
        SetUpStatement(1000.00m, 800.00m, 100.00m, -328.77m); // expected 771.23, statement says 800.00

        var result = await CreateService().ReconcileAsync(Guid.NewGuid());

        Assert.Equal("Mismatch", result.Status);
        Assert.NotNull(result.Discrepancy);
        Assert.NotEqual(0m, result.Discrepancy);
        Assert.Contains("discrepancy", result.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_Tiny_Rounding_Difference_Within_Tolerance_Still_Counts_As_Reconciled()
    {
        SetUpStatement(1000.00m, 771.24m, 100.00m, -328.77m); // off by exactly 0.01

        var result = await CreateService().ReconcileAsync(Guid.NewGuid());

        Assert.Equal("Reconciled", result.Status);
    }

    [Fact]
    public async Task Missing_Opening_Balance_Is_Insufficient_Information_Not_A_Guess()
    {
        SetUpStatement(null, 771.23m, 100.00m, -328.77m);

        var result = await CreateService().ReconcileAsync(Guid.NewGuid());

        Assert.Equal("InsufficientInformation", result.Status);
        Assert.Null(result.ExpectedClosingBalance);
        Assert.Null(result.Discrepancy);
    }

    [Fact]
    public async Task Missing_Closing_Balance_Is_Insufficient_Information_Not_A_Guess()
    {
        SetUpStatement(1000.00m, null, 100.00m, -328.77m);

        var result = await CreateService().ReconcileAsync(Guid.NewGuid());

        Assert.Equal("InsufficientInformation", result.Status);
    }

    [Fact]
    public async Task Persists_One_Result_Row_Per_Reconciliation_Run()
    {
        SetUpStatement(1000.00m, 771.23m, 100.00m, -328.77m);
        var service = CreateService();

        await service.ReconcileAsync(Guid.NewGuid());

        _reconciliationRepository.Verify(r => r.AddAsync(It.IsAny<ReconciliationResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
