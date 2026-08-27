using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FinancialStatementAI.Infrastructure.AI.Classification;
using FinancialStatementAI.Infrastructure.AI.DocumentIntelligence;
using FinancialStatementAI.Infrastructure.BackgroundJobs;
using FinancialStatementAI.Infrastructure.Caching;
using FinancialStatementAI.Infrastructure.Documents;
using FinancialStatementAI.Infrastructure.OCR;
using FinancialStatementAI.Infrastructure.OCR.PaddleOcr;
using FinancialStatementAI.Infrastructure.Persistence;
using FinancialStatementAI.Infrastructure.Repositories;
using FinancialStatementAI.Infrastructure.Security;
using FinancialStatementAI.Infrastructure.Storage;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FinancialStatementAI.Infrastructure;

/// <summary>
/// Composition root for this layer. Api/Worker call this once at startup instead of
/// registering Infrastructure-layer services (persistence, storage, OCR/AI, caching,
/// background jobs) themselves, keeping the hosts unaware of concrete implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddScoped<IStatementRepository, StatementRepository>();
        services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();
        services.AddScoped<IExceptionLogRepository, ExceptionLogRepository>();
        services.AddScoped<IStatementExtractionRepository, StatementExtractionRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IMerchantMappingRepository, MerchantMappingRepository>();
        services.AddScoped<IClassificationHistoryRepository, ClassificationHistoryRepository>();
        services.AddScoped<IAiRequestLogRepository, AiRequestLogRepository>();
        services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
        services.AddScoped<IReconciliationService, Application.Services.ReconciliationService>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IDashboardService, Application.Services.DashboardService>();
        services.AddScoped<IDashboardConfigRepository, DashboardConfigRepository>();
        services.AddScoped<IDashboardConfigService, Application.Services.DashboardConfigService>();
        services.AddSingleton<IStatementFileValidator, StatementFileValidator>();
        services.AddSingleton<IPdfTextExtractionService, PdfTextExtractionService>();
        services.AddSingleton<ITransactionExtractionService, TransactionExtractionService>();
        services.AddSingleton<IStatementFieldExtractionService, StatementFieldExtractionService>();

        services.Configure<LocalFileStorageOptions>(configuration.GetSection(LocalFileStorageOptions.SectionName));
        services.Configure<AzureBlobStorageOptions>(configuration.GetSection(AzureBlobStorageOptions.SectionName));

        // "FileStorage:Provider" = "Azure" switches to Azure Blob Storage (production); anything
        // else (including unset) defaults to local disk storage for development.
        if (string.Equals(configuration["FileStorage:Provider"], "Azure", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorageService, AzureBlobStorageService>();
        }
        else
        {
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        }

        services.Configure<AzureVisionOptions>(configuration.GetSection(AzureVisionOptions.SectionName));
        services.Configure<AzureDocumentIntelligenceOptions>(configuration.GetSection(AzureDocumentIntelligenceOptions.SectionName));
        services.Configure<PaddleOcrOptions>(configuration.GetSection(PaddleOcrOptions.SectionName));
        var paddleOcrOptions = configuration.GetSection(PaddleOcrOptions.SectionName).Get<PaddleOcrOptions>() ?? new PaddleOcrOptions();

        // PaddleOCR (PP-OCRv6, via the separate ocr-service/ Python microservice — see
        // docs/ai-processing.md for why OCR can't run in-process) is the default OCR provider:
        // real, open-source, and never a paid/cloud dependency by default. Set "Ocr:Provider" to
        // "Azure" (plus Azure:Vision:Endpoint/ApiKey) to use Azure Vision's Read feature instead.
        // There is deliberately no "Mock" option anymore — see PaddleOcrService's own doc comment
        // and the test suite's FakeOcrService test double for how tests stay hermetic without one.
        if (string.Equals(configuration["Ocr:Provider"], "Azure", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IOcrService, AzureOcrService>();
        }
        else
        {
            services.AddHttpClient<IOcrService, PaddleOcrService>(client =>
            {
                client.BaseAddress = new Uri(paddleOcrOptions.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(paddleOcrOptions.TimeoutSeconds);
            });
        }

        // PaddleOcr (PP-StructureV3, same ocr-service/ microservice PaddleOcrService talks to) is
        // the default here too, matching Ocr:Provider's default above — StatementProcessingService
        // now actually depends on this for the OCR path (see its ExtractTransactions): OCR'd plain
        // text puts every table cell on its own line, and only the table's reconstructed row/column
        // structure lets transactions be parsed out of a scanned statement at all. Set
        // "DocumentIntelligence:Provider" to "Azure", or to "Mock" to opt back out (e.g. for local
        // dev without ocr-service running — the OCR path still works, just via the line-based
        // parser instead, which finds nothing for OCR'd tabular text as explained above).
        if (string.Equals(configuration["DocumentIntelligence:Provider"], "Azure", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDocumentIntelligenceService, AzureDocumentIntelligenceService>();
        }
        else if (string.Equals(configuration["DocumentIntelligence:Provider"], "Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDocumentIntelligenceService, MockDocumentIntelligenceService>();
        }
        else
        {
            services.AddHttpClient<IDocumentIntelligenceService, PaddleDocumentStructureService>(client =>
            {
                client.BaseAddress = new Uri(paddleOcrOptions.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(paddleOcrOptions.TimeoutSeconds);
            });
        }

        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<AzureOpenAiOptions>(configuration.GetSection(AzureOpenAiOptions.SectionName));

        services.AddScoped<ITransactionClassifier>(sp =>
        {
            var provider = configuration["Classification:Provider"];
            return provider switch
            {
                _ when string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase) =>
                    new OpenAiTransactionClassifier(sp.GetRequiredService<IOptions<OpenAiOptions>>()),
                _ when string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase) =>
                    new AzureOpenAiTransactionClassifier(sp.GetRequiredService<IOptions<AzureOpenAiOptions>>()),
                _ => new MockTransactionClassifier()
            };
        });
        services.AddScoped<ITransactionClassificationService, TransactionClassificationService>();

        // Both default to "Immediate" (works with zero configuration — every existing test
        // exercises this path). Set "BackgroundJobs:Provider" = "Hangfire" to enqueue reprocess
        // requests for a separate FinancialStatementAI.Worker process instead (Phase 14).
        // "Hangfire:Storage" = "InMemory" swaps SQL Server storage for Hangfire's own in-process
        // store — for local dev/tests without a SQL Server instance, never for production.
        if (string.Equals(configuration["BackgroundJobs:Provider"], "Hangfire", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHangfire(config =>
            {
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseFilter(new AutomaticRetryAttribute { Attempts = 3 });

                if (string.Equals(configuration["Hangfire:Storage"], "InMemory", StringComparison.OrdinalIgnoreCase))
                {
                    config.UseInMemoryStorage();
                }
                else
                {
                    config.UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection"));
                }
            });

            services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();
        }
        else
        {
            services.AddScoped<IBackgroundJobScheduler, ImmediateBackgroundJobScheduler>();
        }

        // Both default to in-process implementations (zero configuration — every existing test
        // exercises this path). Set "Caching:Provider" = "Redis" plus
        // "Caching:Redis:ConnectionString" for a real, shared-across-instances Redis backing
        // (Phase 15) — the same switch covers both ICacheService and IDistributedLockService
        // since they share one Redis connection when enabled.
        if (string.Equals(configuration["Caching:Provider"], "Redis", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(configuration["Caching:Redis:ConnectionString"]
                    ?? throw new InvalidOperationException("Caching:Redis:ConnectionString is required when Caching:Provider is Redis.")));
            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
            services.AddSingleton<IDistributedLockService, InMemoryDistributedLockService>();
        }

        return services;
    }

    /// <summary>Starts an actual Hangfire processing server that dequeues and runs jobs — called
    /// only by FinancialStatementAI.Worker, never by the Api host, so a request to the API can
    /// never block on (or crash from) job execution. No-ops when Hangfire isn't the configured
    /// provider, so Worker can call this unconditionally regardless of environment.</summary>
    public static IServiceCollection AddHangfireProcessingServer(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.Equals(configuration["BackgroundJobs:Provider"], "Hangfire", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHangfireServer();
        }

        return services;
    }
}
