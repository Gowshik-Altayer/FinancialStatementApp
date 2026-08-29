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
    ICategoryRepository categoryRepository,
    IReconciliationService reconciliationService) : ITransactionService
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

    public async Task<CorrectTransactionResult> CorrectTransactionAsync(
        Guid transactionId, Guid userId, CorrectTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null || transaction.Statement!.UserId != userId)
        {
            return CorrectTransactionResult.AsNotFound();
        }

        var updates = new TransactionFieldUpdates();
        var corrections = new List<TransactionCorrection>();

        if (!string.IsNullOrWhiteSpace(request.CategoryName))
        {
            var category = await categoryRepository.GetByNameAsync(request.CategoryName, cancellationToken);
            if (category is null)
            {
                return CorrectTransactionResult.Failure($"Unknown category \"{request.CategoryName}\".");
            }

            if (category.Id != transaction.CategoryId)
            {
                updates.CategoryId = category.Id;
                corrections.Add(BuildCorrection(transactionId, userId, CorrectedField.Category, transaction.Category?.Name, category.Name, request.Reason));
            }
        }

        if (request.TransactionDate is { } date && date != transaction.TransactionDate)
        {
            updates.TransactionDate = date;
            corrections.Add(BuildCorrection(
                transactionId, userId, CorrectedField.TransactionDate,
                transaction.TransactionDate?.ToString("yyyy-MM-dd"), date.ToString("yyyy-MM-dd"), request.Reason));
        }

        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description != transaction.Description)
        {
            updates.Description = request.Description;
            corrections.Add(BuildCorrection(transactionId, userId, CorrectedField.Description, transaction.Description, request.Description, request.Reason));
        }

        if (!string.IsNullOrWhiteSpace(request.Merchant) && request.Merchant != transaction.Merchant)
        {
            updates.Merchant = request.Merchant;
            corrections.Add(BuildCorrection(transactionId, userId, CorrectedField.Merchant, transaction.Merchant, request.Merchant, request.Reason));
        }

        if (!string.IsNullOrWhiteSpace(request.TransactionType))
        {
            if (!Enum.TryParse<TransactionType>(request.TransactionType, ignoreCase: true, out var parsedType))
            {
                return CorrectTransactionResult.Failure($"Unknown transaction type \"{request.TransactionType}\".");
            }

            if (parsedType != transaction.TransactionType)
            {
                updates.TransactionType = parsedType;
                corrections.Add(BuildCorrection(
                    transactionId, userId, CorrectedField.TransactionType, transaction.TransactionType.ToString(), parsedType.ToString(), request.Reason));
            }
        }

        if (request.Amount is { } amount && amount != transaction.Amount)
        {
            updates.Amount = amount;
            corrections.Add(BuildCorrection(
                transactionId, userId, CorrectedField.Amount, transaction.Amount?.ToString("0.00"), amount.ToString("0.00"), request.Reason));
        }

        if (corrections.Count == 0)
        {
            // Nothing actually changed (e.g. re-submitting the same category) — return the
            // transaction as-is rather than writing a no-op audit row.
            return CorrectTransactionResult.Success(TransactionMapper.ToResponse(transaction));
        }

        await transactionRepository.ApplyCorrectionAsync(transactionId, updates, corrections, cancellationToken);

        if (updates.Amount is not null)
        {
            // A corrected amount changes the statement's totals — requirement #11's reconciliation
            // must reflect it, not the stale pre-correction numbers.
            await reconciliationService.ReconcileAsync(transaction.StatementId, cancellationToken);
        }

        var updated = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        return CorrectTransactionResult.Success(TransactionMapper.ToResponse(updated!));
    }

    private static TransactionCorrection BuildCorrection(
        Guid transactionId, Guid userId, CorrectedField field, string? originalValue, string correctedValue, string? reason) => new()
    {
        TransactionId = transactionId,
        FieldName = field,
        OriginalValue = originalValue,
        CorrectedValue = correctedValue,
        CorrectedByUserId = userId,
        CorrectionReason = reason
    };

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
