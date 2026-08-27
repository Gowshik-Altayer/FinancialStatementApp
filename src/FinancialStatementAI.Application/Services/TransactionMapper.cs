using FinancialStatementAI.Application.DTOs.Transactions;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Services;

/// <summary>Shared Transaction -> response-DTO mapping, used by TransactionService for both the
/// statement-scoped list and the cross-statement review queue.</summary>
public static class TransactionMapper
{
    public static TransactionResponse ToResponse(Transaction transaction)
    {
        var currentClassification = transaction.Classifications.FirstOrDefault(c => c.IsCurrent);

        return new TransactionResponse
        {
            Id = transaction.Id,
            StatementId = transaction.StatementId,
            StatementFileName = transaction.Statement?.OriginalFileName,
            TransactionDate = transaction.TransactionDate,
            PostingDate = transaction.PostingDate,
            Description = transaction.Description,
            Merchant = transaction.Merchant,
            ReferenceNumber = transaction.ReferenceNumber,
            DebitAmount = transaction.DebitAmount,
            CreditAmount = transaction.CreditAmount,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            TransactionType = transaction.TransactionType.ToString(),
            CategoryId = transaction.CategoryId,
            CategoryName = transaction.Category?.Name,
            ClassificationConfidence = currentClassification?.ConfidenceScore,
            ClassificationMethod = currentClassification?.ClassificationMethod.ToString(),
            ClassificationReason = currentClassification?.Reason,
            ReviewPriority = ReviewPriority(currentClassification?.ConfidenceScore),
            HasBeenCorrected = transaction.Corrections.Count > 0,
            IsPotentialDuplicate = transaction.IsPotentialDuplicate,
            DuplicateOfTransactionId = transaction.DuplicateOfTransactionId,
            Corrections = transaction.Corrections
                .OrderByDescending(c => c.CorrectedAt)
                .Select(ToCorrectionResponse)
                .ToList()
        };
    }

    private static TransactionCorrectionResponse ToCorrectionResponse(TransactionCorrection correction) => new()
    {
        Id = correction.Id,
        FieldName = correction.FieldName.ToString(),
        OriginalValue = correction.OriginalValue,
        CorrectedValue = correction.CorrectedValue,
        CorrectedByUserName = correction.CorrectedByUser is null
            ? null
            : $"{correction.CorrectedByUser.FirstName} {correction.CorrectedByUser.LastName}".Trim(),
        CorrectedAt = correction.CorrectedAt,
        CorrectionReason = correction.CorrectionReason
    };

    /// <summary>Public so DashboardService's confidence-distribution chart buckets transactions
    /// identically to how the review queue itself labels them — one source of truth for what
    /// "high confidence" vs "review required" means.</summary>
    public static string? ReviewPriority(decimal? confidence) => confidence switch
    {
        null => null,
        _ when confidence >= ClassificationConfidenceThresholds.HighConfidenceMinimum => "HighConfidence",
        _ when confidence >= ClassificationConfidenceThresholds.ReviewRecommendedMinimum => "ReviewRecommended",
        _ => "ReviewRequired"
    };
}
