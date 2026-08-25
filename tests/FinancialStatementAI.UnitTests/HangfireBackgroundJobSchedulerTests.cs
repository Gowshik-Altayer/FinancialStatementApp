using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.BackgroundJobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class HangfireBackgroundJobSchedulerTests
{
    private readonly Mock<IBackgroundJobClient> _backgroundJobClient = new();
    private readonly Mock<IStatementRepository> _statementRepository = new();
    private readonly Mock<IProcessingJobRepository> _processingJobRepository = new();

    private HangfireBackgroundJobScheduler CreateScheduler() => new(_backgroundJobClient.Object, _statementRepository.Object, _processingJobRepository.Object);

    [Fact]
    public async Task Enqueues_A_Call_To_ProcessAsync_On_IStatementProcessingService()
    {
        var statementId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        Job? capturedJob = null;
        _backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, _) => capturedJob = job)
            .Returns("hangfire-job-42");

        await CreateScheduler().EnqueueStatementReprocessAsync(statementId, userId);

        Assert.NotNull(capturedJob);
        Assert.Equal(typeof(IStatementProcessingService), capturedJob!.Type);
        Assert.Equal(nameof(IStatementProcessingService.ProcessAsync), capturedJob.Method.Name);
        Assert.Equal(statementId, capturedJob.Args[0]);
        Assert.Equal(userId, capturedJob.Args[1]);
    }

    [Fact]
    public async Task Flips_The_Statement_To_Processing_Before_The_Job_Even_Runs()
    {
        var statementId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _backgroundJobClient.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("hangfire-job-42");

        await CreateScheduler().EnqueueStatementReprocessAsync(statementId, userId);

        _statementRepository.Verify(
            r => r.UpdateStatusAsync(statementId, StatementProcessingStatus.Processing, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Records_A_Pending_ProcessingJob_Row_With_The_Hangfire_Job_Id()
    {
        var statementId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _backgroundJobClient.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("hangfire-job-42");

        ProcessingJob? captured = null;
        _processingJobRepository
            .Setup(r => r.AddAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessingJob, CancellationToken>((job, _) => captured = job)
            .Returns(Task.CompletedTask);

        await CreateScheduler().EnqueueStatementReprocessAsync(statementId, userId);

        Assert.NotNull(captured);
        Assert.Equal("hangfire-job-42", captured!.HangfireJobId);
        Assert.Equal(ProcessingJobStatus.Pending, captured.Status);
        Assert.Equal(statementId, captured.StatementId);
    }
}
