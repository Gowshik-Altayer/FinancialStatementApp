namespace FinancialStatementAI.Application.Interfaces;

public interface ITransactionClassificationService
{
    Task ClassifyStatementTransactionsAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);
}
