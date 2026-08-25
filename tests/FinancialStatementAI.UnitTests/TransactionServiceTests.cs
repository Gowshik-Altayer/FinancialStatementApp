using FinancialStatementAI.Application.DTOs.Transactions;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _transactionRepository = new();
    private readonly Mock<IStatementRepository> _statementRepository = new();
    private readonly Mock<ICategoryRepository> _categoryRepository = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid StatementId = Guid.NewGuid();
    private static readonly Guid TransactionId = Guid.NewGuid();

    private TransactionService CreateService() => new(_transactionRepository.Object, _statementRepository.Object, _categoryRepository.Object);

    private static Transaction BuildTransaction(Guid userId, Category? category = null, params TransactionClassification[] classifications)
    {
        var statement = new Statement { Id = StatementId, UserId = userId, OriginalFileName = "statement.pdf" };
        return new Transaction
        {
            Id = TransactionId,
            StatementId = StatementId,
            Statement = statement,
            Description = "GROCERY STORE",
            Merchant = "GROCERY STORE",
            Amount = -45.67m,
            TransactionType = TransactionType.Debit,
            Category = category,
            CategoryId = category?.Id,
            Classifications = classifications,
            Corrections = []
        };
    }

    [Fact]
    public async Task GetForStatementAsync_Returns_Null_When_Statement_Belongs_To_Another_User()
    {
        _statementRepository.Setup(r => r.GetByIdAsync(StatementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Statement { Id = StatementId, UserId = Guid.NewGuid() });

        var result = await CreateService().GetForStatementAsync(StatementId, UserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetForStatementAsync_Maps_Current_Classification_And_Review_Priority()
    {
        var category = new Category { Name = "Groceries" };
        var classification = new TransactionClassification
        {
            Category = category,
            ConfidenceScore = 0.55m,
            ClassificationMethod = ClassificationMethod.Llm,
            IsCurrent = true
        };
        var transaction = BuildTransaction(UserId, category, classification);

        _statementRepository.Setup(r => r.GetByIdAsync(StatementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Statement { Id = StatementId, UserId = UserId });
        _transactionRepository.Setup(r => r.GetByStatementIdAsync(StatementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([transaction]);

        var result = await CreateService().GetForStatementAsync(StatementId, UserId);

        var response = Assert.Single(result!);
        Assert.Equal("Groceries", response.CategoryName);
        Assert.Equal(0.55m, response.ClassificationConfidence);
        Assert.Equal("ReviewRequired", response.ReviewPriority); // below the 0.60 ReviewRecommendedMinimum
        Assert.False(response.HasBeenCorrected);
    }

    [Fact]
    public async Task CorrectCategoryAsync_Returns_NotFound_For_Another_Users_Transaction()
    {
        _transactionRepository.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTransaction(Guid.NewGuid()));

        var result = await CreateService().CorrectCategoryAsync(TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Groceries" });

        Assert.True(result.NotFound);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CorrectCategoryAsync_Rejects_An_Unknown_Category_Name()
    {
        _transactionRepository.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTransaction(UserId));
        _categoryRepository.Setup(r => r.GetByNameAsync("Not A Real Category", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var result = await CreateService().CorrectCategoryAsync(TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Not A Real Category" });

        Assert.False(result.Succeeded);
        Assert.False(result.NotFound);
        Assert.Contains("Not A Real Category", result.Error);
        _transactionRepository.Verify(r => r.ApplyCorrectionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TransactionCorrection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CorrectCategoryAsync_Records_The_Original_Ai_Category_Before_Overwriting_It()
    {
        var originalCategory = new Category { Id = Guid.NewGuid(), Name = "Other" };
        var correctedCategory = new Category { Id = Guid.NewGuid(), Name = "Groceries" };
        var transactionBeforeCorrection = BuildTransaction(UserId, originalCategory);

        _transactionRepository.SetupSequence(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionBeforeCorrection)
            .ReturnsAsync(BuildTransaction(UserId, correctedCategory));
        _categoryRepository.Setup(r => r.GetByNameAsync("Groceries", It.IsAny<CancellationToken>()))
            .ReturnsAsync(correctedCategory);

        TransactionCorrection? capturedCorrection = null;
        _transactionRepository
            .Setup(r => r.ApplyCorrectionAsync(TransactionId, correctedCategory.Id, It.IsAny<TransactionCorrection>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, TransactionCorrection, CancellationToken>((_, _, correction, _) => capturedCorrection = correction)
            .Returns(Task.CompletedTask);

        var result = await CreateService().CorrectCategoryAsync(
            TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Groceries", Reason = "Wrong merchant match" });

        Assert.True(result.Succeeded);
        Assert.Equal("Groceries", result.Transaction!.CategoryName);
        Assert.NotNull(capturedCorrection);
        Assert.Equal(CorrectedField.Category, capturedCorrection!.FieldName);
        Assert.Equal("Other", capturedCorrection.OriginalValue); // the AI's original category, preserved
        Assert.Equal("Groceries", capturedCorrection.CorrectedValue);
        Assert.Equal(UserId, capturedCorrection.CorrectedByUserId);
        Assert.Equal("Wrong merchant match", capturedCorrection.CorrectionReason);
    }
}
