using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FluentAssertions;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class TransactionClassificationServiceTests
{
    private readonly Mock<ITransactionRepository> _transactionRepository = new();
    private readonly Mock<ICategoryRepository> _categoryRepository = new();
    private readonly Mock<IMerchantMappingRepository> _merchantMappingRepository = new();
    private readonly Mock<IClassificationHistoryRepository> _classificationHistoryRepository = new();
    private readonly Mock<ITransactionClassifier> _transactionClassifier = new();
    private readonly Mock<IAiRequestLogRepository> _aiRequestLogRepository = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid StatementId = Guid.NewGuid();

    private readonly List<Category> _categories =
    [
        new() { Name = "Transportation" },
        new() { Name = "Groceries" },
        new() { Name = "Other" }
    ];

    private TransactionClassificationService CreateService()
    {
        _categoryRepository.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_categories);
        // Explicit "no match" defaults so a test exercising an earlier rung of the ladder never
        // silently falls through into an unconfigured mock (which would NRE rather than fail
        // with a clear assertion message).
        _merchantMappingRepository.Setup(r => r.FindMatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((MerchantMapping?)null);
        _classificationHistoryRepository.Setup(r => r.FindPreviousCorrectedCategoryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _transactionClassifier
            .Setup(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClassificationResult.Failure("not configured for this test"));

        return new TransactionClassificationService(
            _transactionRepository.Object,
            _categoryRepository.Object,
            _merchantMappingRepository.Object,
            _classificationHistoryRepository.Object,
            _transactionClassifier.Object,
            _aiRequestLogRepository.Object);
    }

    private Transaction SetUpSingleTransaction(string? merchant, string description)
    {
        var transaction = new Transaction { Id = Guid.NewGuid(), StatementId = StatementId, Merchant = merchant, Description = description };
        _transactionRepository.Setup(r => r.GetByStatementIdAsync(StatementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([transaction]);
        return transaction;
    }

    [Fact]
    public async Task A_Keyword_Rule_Match_Wins_Without_Ever_Calling_The_Merchant_Mapping_Or_Llm()
    {
        SetUpSingleTransaction("SOME BANK", "PAYROLL DEPOSIT FROM EMPLOYER");
        _categories.Add(new Category { Name = "Payroll" });

        var service = CreateService();
        await service.ClassifyStatementTransactionsAsync(StatementId, UserId);

        _merchantMappingRepository.Verify(r => r.FindMatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionClassifier.Verify(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(r => r.ApplyClassificationAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.Is<decimal>(c => c >= 0.90m),
            ClassificationMethod.Rule,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Merchant_Mapping_Match_Wins_Over_The_Llm_When_No_Rule_Matches()
    {
        var transaction = SetUpSingleTransaction("UBER TRIP 12345", "UBER TRIP 12345");
        var transportationCategory = _categories.First(c => c.Name == "Transportation");
        var service = CreateService();
        // Configured after CreateService() deliberately — Moq's "last setup wins" rule means an
        // override configured before CreateService() would be clobbered by its generic defaults.
        _merchantMappingRepository.Setup(r => r.FindMatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MerchantMapping { MerchantPattern = "UBER", Category = transportationCategory, CategoryId = transportationCategory.Id });

        await service.ClassifyStatementTransactionsAsync(StatementId, UserId);

        _transactionClassifier.Verify(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(r => r.ApplyClassificationAsync(
            transaction.Id, transportationCategory.Id, It.IsAny<decimal>(), ClassificationMethod.MerchantMapping, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Previous_Human_Correction_For_The_Same_Merchant_Wins_Over_The_Llm()
    {
        SetUpSingleTransaction("ACME SERVICES", "ACME SERVICES PAYMENT");
        var service = CreateService();
        _classificationHistoryRepository
            .Setup(r => r.FindPreviousCorrectedCategoryAsync(UserId, "ACME SERVICES", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Groceries");

        await service.ClassifyStatementTransactionsAsync(StatementId, UserId);

        _transactionClassifier.Verify(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        var groceriesId = _categories.First(c => c.Name == "Groceries").Id;
        _transactionRepository.Verify(r => r.ApplyClassificationAsync(
            It.IsAny<Guid>(), groceriesId, It.IsAny<decimal>(), ClassificationMethod.PreviousCorrection, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Falls_Through_To_The_Llm_When_No_Rule_Mapping_Or_History_Matches()
    {
        SetUpSingleTransaction("SOME UNKNOWN MERCHANT", "SOME UNKNOWN MERCHANT PURCHASE");
        var service = CreateService();
        _transactionClassifier
            .Setup(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClassificationResult.Success("Groceries", 0.72m, "Looks like a grocery purchase"));

        await service.ClassifyStatementTransactionsAsync(StatementId, UserId);

        var groceriesId = _categories.First(c => c.Name == "Groceries").Id;
        _transactionRepository.Verify(r => r.ApplyClassificationAsync(
            It.IsAny<Guid>(), groceriesId, 0.72m, ClassificationMethod.Llm, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _aiRequestLogRepository.Verify(r => r.AddAsync(It.Is<AIRequest>(req => req.IsSuccess), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_Invalid_Category_From_The_Llm_Is_Never_Trusted_And_Falls_Back_To_Other()
    {
        SetUpSingleTransaction("SOME UNKNOWN MERCHANT", "SOME UNKNOWN MERCHANT PURCHASE");
        var service = CreateService();
        _transactionClassifier
            .Setup(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClassificationResult.Success("Cryptocurrency Trading", 0.95m, "invented category not in our list"));

        await service.ClassifyStatementTransactionsAsync(StatementId, UserId);

        var otherId = _categories.First(c => c.Name == "Other").Id;
        _transactionRepository.Verify(r => r.ApplyClassificationAsync(
            It.IsAny<Guid>(),
            otherId,
            It.Is<decimal>(confidence => confidence < 0.60m), // must be forced into "review required" territory
            ClassificationMethod.Llm,
            It.Is<string>(reason => reason!.Contains("unrecognized category")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Failed_Llm_Call_Still_Records_A_Low_Confidence_Other_Classification_Rather_Than_Throwing()
    {
        SetUpSingleTransaction("SOME UNKNOWN MERCHANT", "SOME UNKNOWN MERCHANT PURCHASE");
        var service = CreateService();
        _transactionClassifier
            .Setup(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClassificationResult.Failure("API timeout"));

        var act = async () => await service.ClassifyStatementTransactionsAsync(StatementId, UserId);

        await act.Should().NotThrowAsync();
        var otherId = _categories.First(c => c.Name == "Other").Id;
        _transactionRepository.Verify(r => r.ApplyClassificationAsync(
            It.IsAny<Guid>(), otherId, 0m, ClassificationMethod.Llm, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _aiRequestLogRepository.Verify(r => r.AddAsync(It.Is<AIRequest>(req => !req.IsSuccess), It.IsAny<CancellationToken>()), Times.Once);
    }
}
