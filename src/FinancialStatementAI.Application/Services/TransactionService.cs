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

    public async Task<PagedResult<TransactionResponse>> SearchAsync(
        Guid userId,
        string? search,
        Guid? categoryId,
        Guid? statementId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, PaginationDefaults.MaxPageSize);

        var result = await transactionRepository.SearchAsync(userId, search, categoryId, statementId, page, pageSize, cancellationToken);
        var items = result.Items.Select(TransactionMapper.ToResponse).ToList();

        return PagedResult<TransactionResponse>.Create(items, result.TotalCount, result.Page, result.PageSize);
    }

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
}
