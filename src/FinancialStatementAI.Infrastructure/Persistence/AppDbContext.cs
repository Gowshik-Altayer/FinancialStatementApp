using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Statement> Statements => Set<Statement>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MerchantMapping> MerchantMappings => Set<MerchantMapping>();
    public DbSet<StatementExtraction> StatementExtractions => Set<StatementExtraction>();
    public DbSet<TransactionExtraction> TransactionExtractions => Set<TransactionExtraction>();
    public DbSet<TransactionClassification> TransactionClassifications => Set<TransactionClassification>();
    public DbSet<TransactionCorrection> TransactionCorrections => Set<TransactionCorrection>();
    public DbSet<ReconciliationResult> ReconciliationResults => Set<ReconciliationResult>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<ProcessingError> ProcessingErrors => Set<ProcessingError>();
    public DbSet<AIRequest> AIRequests => Set<AIRequest>();
    public DbSet<AIUsageMetric> AIUsageMetrics => Set<AIUsageMetric>();
    public DbSet<ExceptionLog> ExceptionLogs => Set<ExceptionLog>();
    public DbSet<OcrTextBlock> OcrTextBlocks => Set<OcrTextBlock>();
    public DbSet<OcrTableRegion> OcrTableRegions => Set<OcrTableRegion>();
    public DbSet<DashboardWidgetPreference> DashboardWidgetPreferences => Set<DashboardWidgetPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
