using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Transactions;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

public class TransactionService(
    ITransactionRepository transactionRepository,
    IStatementRepository statementRepository,
    ICategoryRepository categoryRepository) : ITransactionService
{
    public async Task<IReadOnlyList<TransactionResponse>?> GetForStatementAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        if (statement is null || statement.UserId != userId)
        {
            return null;
        }

        var transactions = await transactionRepository.GetByStatementIdAsync(statementId, cancellationToken);
        return transactions.Select(TransactionMapper.ToResponse).ToList();
    }

    public async Task<IReadOnlyList<TransactionResponse>> GetReviewQueueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetReviewQueueAsync(userId, cancellationToken);
        return transactions.Select(TransactionMapper.ToResponse).ToList();
    }

    public async Task<PagedResult<TransactionResponse>> SearchAsync(Guid userId, TransactionSearchFilter filter, CancellationToken cancellationToken = default)
    {
        filter.Page = Math.Max(1, filter.Page);
        filter.PageSize = Math.Clamp(filter.PageSize == 0 ? PaginationDefaults.DefaultPageSize : filter.PageSize, 1, PaginationDefaults.MaxPageSize);

        var result = await transactionRepository.SearchAsync(userId, filter, cancellationToken);
        var items = result.Items.Select(TransactionMapper.ToResponse).ToList();

        return PagedResult<TransactionResponse>.Create(items, result.TotalCount, result.Page, result.PageSize);
    }

    public Task<TransactionSummaryResponse> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default) =>
        transactionRepository.GetSummaryAsync(userId, cancellationToken);

    public async Task<CorrectTransactionResult> CorrectCategoryAsync(
        Guid transactionId, Guid userId, CorrectTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            return CorrectTransactionResult.Failure("categoryName is required.");
        }

        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null || transaction.Statement!.UserId != userId)
        {
            return CorrectTransactionResult.AsNotFound();
        }

        var category = await categoryRepository.GetByNameAsync(request.CategoryName, cancellationToken);
        if (category is null)
        {
            return CorrectTransactionResult.Failure($"Unknown category \"{request.CategoryName}\".");
        }

        var correction = new TransactionCorrection
        {
            TransactionId = transactionId,
            FieldName = CorrectedField.Category,
            OriginalValue = transaction.Category?.Name,
            CorrectedValue = category.Name,
            CorrectedByUserId = userId,
            CorrectionReason = request.Reason
        };

        await transactionRepository.ApplyCorrectionAsync(transactionId, category.Id, correction, cancellationToken);

        var updated = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        return CorrectTransactionResult.Success(TransactionMapper.ToResponse(updated!));
    }

    public async Task<BulkCorrectTransactionResult> BulkCorrectCategoryAsync(
        Guid anchorTransactionId, Guid userId, CorrectTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            return BulkCorrectTransactionResult.Failure("categoryName is required.");
        }

        var anchor = await transactionRepository.GetByIdAsync(anchorTransactionId, cancellationToken);
        if (anchor is null || anchor.Statement!.UserId != userId)
        {
            return BulkCorrectTransactionResult.AsNotFound();
        }

        if (string.IsNullOrWhiteSpace(anchor.Merchant))
        {
            // Every extraction path populates Merchant (falling back to Description when no
            // separate merchant name exists — see ParsedTransaction's doc comment), so this is a
            // genuine edge case rather than the common path. Refusing here rather than silently
            // falling back to Description keeps "bulk update by merchant" meaning exactly what it
            // says instead of quietly grouping by a different field.
            return BulkCorrectTransactionResult.Failure("This transaction has no merchant name to group by.");
        }

        var category = await categoryRepository.GetByNameAsync(request.CategoryName, cancellationToken);
        if (category is null)
        {
            return BulkCorrectTransactionResult.Failure($"Unknown category \"{request.CategoryName}\".");
        }

        var updatedCount = await transactionRepository.ApplyBulkCorrectionByMerchantAsync(
            userId, anchor.Merchant, category.Id, category.Name, request.Reason, userId, cancellationToken);

        var updatedAnchor = await transactionRepository.GetByIdAsync(anchorTransactionId, cancellationToken);
        return BulkCorrectTransactionResult.Success(updatedCount, TransactionMapper.ToResponse(updatedAnchor!));
    }
}
