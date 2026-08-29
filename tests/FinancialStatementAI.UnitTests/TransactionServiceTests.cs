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
    private readonly Mock<IReconciliationService> _reconciliationService = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid StatementId = Guid.NewGuid();
    private static readonly Guid TransactionId = Guid.NewGuid();

    private TransactionService CreateService() =>
        new(_transactionRepository.Object, _statementRepository.Object, _categoryRepository.Object, _reconciliationService.Object);

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

    private void SetUpApplyCorrectionCapture(Action<TransactionFieldUpdates, IReadOnlyList<TransactionCorrection>> capture) =>
        _transactionRepository
            .Setup(r => r.ApplyCorrectionAsync(TransactionId, It.IsAny<TransactionFieldUpdates>(), It.IsAny<IReadOnlyList<TransactionCorrection>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, TransactionFieldUpdates, IReadOnlyList<TransactionCorrection>, CancellationToken>((_, updates, corrections, _) => capture(updates, corrections))
            .Returns(Task.CompletedTask);

    [Fact]
    public async Task CorrectTransactionAsync_Returns_NotFound_For_Another_Users_Transaction()
    {
        _transactionRepository.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTransaction(Guid.NewGuid()));

        var result = await CreateService().CorrectTransactionAsync(TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Groceries" });

        Assert.True(result.NotFound);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CorrectTransactionAsync_Rejects_An_Unknown_Category_Name()
    {
        _transactionRepository.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTransaction(UserId));
        _categoryRepository.Setup(r => r.GetByNameAsync("Not A Real Category", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var result = await CreateService().CorrectTransactionAsync(TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Not A Real Category" });

        Assert.False(result.Succeeded);
        Assert.False(result.NotFound);
        Assert.Contains("Not A Real Category", result.Error);
        _transactionRepository.Verify(
            r => r.ApplyCorrectionAsync(It.IsAny<Guid>(), It.IsAny<TransactionFieldUpdates>(), It.IsAny<IReadOnlyList<TransactionCorrection>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CorrectTransactionAsync_Records_The_Original_Ai_Category_Before_Overwriting_It()
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
        SetUpApplyCorrectionCapture((_, corrections) => capturedCorrection = Assert.Single(corrections));

        var result = await CreateService().CorrectTransactionAsync(
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

    [Fact]
    public async Task CorrectTransactionAsync_Applies_Several_Fields_At_Once_Each_With_Its_Own_Audit_Row()
    {
        var transactionBeforeCorrection = BuildTransaction(UserId);

        _transactionRepository.SetupSequence(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionBeforeCorrection)
            .ReturnsAsync(BuildTransaction(UserId));

        TransactionFieldUpdates? capturedUpdates = null;
        IReadOnlyList<TransactionCorrection>? capturedCorrections = null;
        SetUpApplyCorrectionCapture((updates, corrections) =>
        {
            capturedUpdates = updates;
            capturedCorrections = corrections;
        });

        var request = new CorrectTransactionRequest
        {
            TransactionDate = new DateOnly(2026, 3, 15),
            Description = "CORRECTED DESCRIPTION",
            Merchant = "CORRECTED MERCHANT",
            Amount = -99.99m,
            TransactionType = "Refund"
        };

        var result = await CreateService().CorrectTransactionAsync(TransactionId, UserId, request);

        Assert.True(result.Succeeded);
        Assert.NotNull(capturedUpdates);
        Assert.Equal(new DateOnly(2026, 3, 15), capturedUpdates!.TransactionDate);
        Assert.Equal("CORRECTED DESCRIPTION", capturedUpdates.Description);
        Assert.Equal("CORRECTED MERCHANT", capturedUpdates.Merchant);
        Assert.Equal(-99.99m, capturedUpdates.Amount);
        Assert.Equal(TransactionType.Refund, capturedUpdates.TransactionType);
        Assert.Null(capturedUpdates.CategoryId); // not part of this request — left untouched

        // Five fields corrected => five distinct audit rows, matching TransactionCorrection's own
        // "one row per corrected field" contract.
        Assert.Equal(5, capturedCorrections!.Count);
        Assert.Contains(capturedCorrections, c => c.FieldName == CorrectedField.TransactionDate);
        Assert.Contains(capturedCorrections, c => c.FieldName == CorrectedField.Description);
        Assert.Contains(capturedCorrections, c => c.FieldName == CorrectedField.Merchant);
        Assert.Contains(capturedCorrections, c => c.FieldName == CorrectedField.Amount);
        Assert.Contains(capturedCorrections, c => c.FieldName == CorrectedField.TransactionType);
    }

    [Fact]
    public async Task CorrectTransactionAsync_Rejects_An_Unknown_Transaction_Type()
    {
        _transactionRepository.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTransaction(UserId));

        var result = await CreateService().CorrectTransactionAsync(
            TransactionId, UserId, new CorrectTransactionRequest { TransactionType = "NotARealType" });

        Assert.False(result.Succeeded);
        Assert.Contains("NotARealType", result.Error);
    }

    [Fact]
    public async Task CorrectTransactionAsync_Re_Reconciles_The_Statement_When_Amount_Changes()
    {
        _transactionRepository.SetupSequence(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTransaction(UserId))
            .ReturnsAsync(BuildTransaction(UserId));

        await CreateService().CorrectTransactionAsync(TransactionId, UserId, new CorrectTransactionRequest { Amount = -50.00m });

        _reconciliationService.Verify(r => r.ReconcileAsync(StatementId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CorrectTransactionAsync_Does_Not_Reconcile_When_Only_Category_Changes()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Groceries" };
        _transactionRepository.SetupSequence(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTransaction(UserId))
            .ReturnsAsync(BuildTransaction(UserId, category));
        _categoryRepository.Setup(r => r.GetByNameAsync("Groceries", It.IsAny<CancellationToken>())).ReturnsAsync(category);

        await CreateService().CorrectTransactionAsync(TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Groceries" });

        _reconciliationService.Verify(r => r.ReconcileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CorrectTransactionAsync_Is_A_No_Op_When_Nothing_Actually_Changed()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Groceries" };
        var transaction = BuildTransaction(UserId, category);
        _transactionRepository.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>())).ReturnsAsync(transaction);
        _categoryRepository.Setup(r => r.GetByNameAsync("Groceries", It.IsAny<CancellationToken>())).ReturnsAsync(category);

        // Re-submitting the transaction's current category — nothing actually changes.
        var result = await CreateService().CorrectTransactionAsync(TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Groceries" });

        Assert.True(result.Succeeded);
        _transactionRepository.Verify(
            r => r.ApplyCorrectionAsync(It.IsAny<Guid>(), It.IsAny<TransactionFieldUpdates>(), It.IsAny<IReadOnlyList<TransactionCorrection>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // --- BulkCorrectCategoryAsync: the "apply to all transactions from this merchant" path ------

    [Fact]
    public async Task BulkCorrectCategoryAsync_Returns_NotFound_For_Another_Users_Transaction()
    {
        _transactionRepository.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTransaction(Guid.NewGuid()));

        var result = await CreateService().BulkCorrectCategoryAsync(TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Groceries" });

        Assert.True(result.NotFound);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task BulkCorrectCategoryAsync_Rejects_An_Unknown_Category_Name()
    {
        _transactionRepository.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTransaction(UserId));
        _categoryRepository.Setup(r => r.GetByNameAsync("Not A Real Category", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var result = await CreateService().BulkCorrectCategoryAsync(TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Not A Real Category" });

        Assert.False(result.Succeeded);
        _transactionRepository.Verify(
            r => r.ApplyBulkCorrectionByMerchantAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BulkCorrectCategoryAsync_Refuses_To_Group_By_A_Missing_Merchant()
    {
        var transaction = BuildTransaction(UserId);
        transaction.Merchant = null;
        _transactionRepository.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>())).ReturnsAsync(transaction);

        var result = await CreateService().BulkCorrectCategoryAsync(TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Groceries" });

        Assert.False(result.Succeeded);
        Assert.False(result.NotFound);
    }

    [Fact]
    public async Task BulkCorrectCategoryAsync_Applies_The_Correction_By_Merchant_And_Reports_The_Updated_Count()
    {
        var correctedCategory = new Category { Id = Guid.NewGuid(), Name = "Groceries" };
        var anchor = BuildTransaction(UserId);

        _transactionRepository.SetupSequence(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(anchor)
            .ReturnsAsync(BuildTransaction(UserId, correctedCategory));
        _categoryRepository.Setup(r => r.GetByNameAsync("Groceries", It.IsAny<CancellationToken>())).ReturnsAsync(correctedCategory);
        _transactionRepository
            .Setup(r => r.ApplyBulkCorrectionByMerchantAsync(
                UserId, "GROCERY STORE", correctedCategory.Id, "Groceries", "Same merchant", UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await CreateService().BulkCorrectCategoryAsync(
            TransactionId, UserId, new CorrectTransactionRequest { CategoryName = "Groceries", Reason = "Same merchant" });

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.UpdatedCount);
        Assert.Equal("Groceries", result.Transaction!.CategoryName);
    }
}
