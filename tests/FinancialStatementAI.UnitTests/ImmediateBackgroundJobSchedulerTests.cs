using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.BackgroundJobs;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class ImmediateBackgroundJobSchedulerTests
{
    private readonly Mock<IStatementProcessingService> _processingService = new();
    private readonly Mock<IProcessingJobRepository> _processingJobRepository = new();

    private ImmediateBackgroundJobScheduler CreateScheduler() => new(_processingService.Object, _processingJobRepository.Object);

    [Fact]
    public async Task Runs_The_Pipeline_Synchronously_And_Records_A_Succeeded_Job()
    {
        var statementId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _processingService.Setup(p => p.ProcessAsync(statementId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StatementDetailResponse());

        ProcessingJob? captured = null;
        _processingJobRepository.Setup(r => r.AddAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessingJob, CancellationToken>((job, _) => captured = job)
            .Returns(Task.CompletedTask);

        await CreateScheduler().EnqueueStatementReprocessAsync(statementId, userId);

        _processingService.Verify(p => p.ProcessAsync(statementId, userId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        _processingJobRepository.Verify(r => r.MarkStatusAsync(captured!.Id, ProcessingJobStatus.Succeeded, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Pipeline_Failure_Is_Recorded_As_Failed_And_Rethrown()
    {
        var statementId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _processingService.Setup(p => p.ProcessAsync(statementId, userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        ProcessingJob? captured = null;
        _processingJobRepository.Setup(r => r.AddAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessingJob, CancellationToken>((job, _) => captured = job)
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateScheduler().EnqueueStatementReprocessAsync(statementId, userId));

        Assert.NotNull(captured);
        _processingJobRepository.Verify(r => r.MarkStatusAsync(captured!.Id, ProcessingJobStatus.Failed, It.IsAny<CancellationToken>()), Times.Once);
    }
}
