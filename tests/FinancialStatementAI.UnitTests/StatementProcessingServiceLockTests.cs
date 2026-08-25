using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FinancialStatementAI.Domain.Entities;
using Moq;

namespace FinancialStatementAI.UnitTests;

/// <summary>Phase 15's one new piece of logic in this class: a second, overlapping ProcessAsync
/// call for the same statement must not run the pipeline again — see
/// docs/architecture.md's "Hangfire background processing" section for why this became a real
/// risk once Phase 14 made this callable from a background worker. The happy (lock acquired)
/// path is already covered end-to-end by the integration tests in StatementReprocessTests etc.</summary>
public class StatementProcessingServiceLockTests
{
    private readonly Mock<IStatementRepository> _statementRepository = new();
    private readonly Mock<IStatementExtractionRepository> _statementExtractionRepository = new();
    private readonly Mock<ITransactionRepository> _transactionRepository = new();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IPdfTextExtractionService> _pdfTextExtractionService = new();
    private readonly Mock<IOcrService> _ocrService = new();
    private readonly Mock<IStatementFieldExtractionService> _statementFieldExtractionService = new();
    private readonly Mock<ITransactionExtractionService> _transactionExtractionService = new();
    private readonly Mock<ITransactionClassificationService> _transactionClassificationService = new();
    private readonly Mock<IReconciliationService> _reconciliationService = new();
    private readonly Mock<IDistributedLockService> _distributedLockService = new();

    private StatementProcessingService CreateService() => new(
        _statementRepository.Object,
        _statementExtractionRepository.Object,
        _transactionRepository.Object,
        _fileStorage.Object,
        _pdfTextExtractionService.Object,
        _ocrService.Object,
        _statementFieldExtractionService.Object,
        _transactionExtractionService.Object,
        _transactionClassificationService.Object,
        _reconciliationService.Object,
        _distributedLockService.Object);

    [Fact]
    public async Task An_Already_In_Flight_Reprocess_Short_Circuits_Without_Touching_The_Pipeline()
    {
        var statementId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var statement = new Statement { Id = statementId, UserId = userId, OriginalFileName = "statement.pdf" };

        _statementRepository.Setup(r => r.GetByIdAsync(statementId, It.IsAny<CancellationToken>())).ReturnsAsync(statement);
        _distributedLockService
            .Setup(l => l.TryAcquireAsync(It.Is<string>(s => s.Contains(statementId.ToString())), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAsyncDisposable?)null);

        var result = await CreateService().ProcessAsync(statementId, userId);

        Assert.NotNull(result);
        Assert.Equal(statementId, result!.Id);
        _fileStorage.Verify(f => f.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(
            r => r.ReplaceForStatementAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<Transaction>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _transactionClassificationService.Verify(
            c => c.ClassifyStatementTransactionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _reconciliationService.Verify(r => r.ReconcileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _statementRepository.Verify(
            r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<Domain.Enums.StatementProcessingStatus>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_Successfully_Acquired_Lock_Is_Released_Even_When_Held()
    {
        var statementId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var statement = new Statement { Id = statementId, UserId = userId, OriginalFileName = "statement.pdf", ContentType = "image/png" };

        _statementRepository.Setup(r => r.GetByIdAsync(statementId, It.IsAny<CancellationToken>())).ReturnsAsync(statement);

        var lockHandle = new Mock<IAsyncDisposable>();
        _distributedLockService
            .Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockHandle.Object);
        _ocrService.Setup(o => o.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.DTOs.Statements.OcrResult.Failure("not configured for this test"));
        _fileStorage.Setup(f => f.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Stream.Null);

        await CreateService().ProcessAsync(statementId, userId);

        lockHandle.Verify(l => l.DisposeAsync(), Times.Once);
    }
}
