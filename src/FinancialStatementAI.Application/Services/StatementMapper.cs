using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Services;

/// <summary>Shared Statement -> response-DTO mapping, used by both StatementService (reads) and
/// StatementProcessingService (writes back the updated statement after processing) so the two
/// don't drift out of sync with slightly different projections.</summary>
public static class StatementMapper
{
    public static StatementSummaryResponse ToSummaryResponse(Statement statement) => new()
    {
        Id = statement.Id,
        OriginalFileName = statement.OriginalFileName,
        ProviderName = statement.ProviderName,
        StatementPeriodStart = statement.StatementPeriodStart,
        StatementPeriodEnd = statement.StatementPeriodEnd,
        TransactionCount = statement.Transactions.Count,
        TotalDebits = statement.TotalDebits,
        TotalCredits = statement.TotalCredits,
        ProcessingStatus = statement.ProcessingStatus.ToString(),
        UploadedAt = statement.UploadedAt
    };

    public static StatementDetailResponse ToDetailResponse(Statement statement) => new()
    {
        Id = statement.Id,
        OriginalFileName = statement.OriginalFileName,
        ContentType = statement.ContentType,
        FileSizeBytes = statement.FileSizeBytes,
        DocumentType = statement.DocumentType.ToString(),
        AccountHolderName = statement.AccountHolderName,
        ProviderName = statement.ProviderName,
        AccountNumberMasked = statement.AccountNumberMasked,
        StatementPeriodStart = statement.StatementPeriodStart,
        StatementPeriodEnd = statement.StatementPeriodEnd,
        StatementDate = statement.StatementDate,
        OpeningBalance = statement.OpeningBalance,
        ClosingBalance = statement.ClosingBalance,
        TotalDebits = statement.TotalDebits,
        TotalCredits = statement.TotalCredits,
        TotalPayments = statement.TotalPayments,
        TotalPurchases = statement.TotalPurchases,
        Currency = statement.Currency,
        ProcessingStatus = statement.ProcessingStatus.ToString(),
        UploadedAt = statement.UploadedAt,
        ProcessedAt = statement.ProcessedAt,
        TransactionCount = statement.Transactions.Count,
        HasUsableText = statement.StatementExtraction?.HasUsableText,
        ExtractedPageCount = statement.StatementExtraction?.PageCount,
        ExtractionMethod = statement.StatementExtraction?.ExtractionMethod.ToString()
    };
}
