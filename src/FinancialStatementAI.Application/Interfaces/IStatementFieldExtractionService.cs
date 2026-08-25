using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementFieldExtractionService
{
    ExtractedStatementFields Extract(string rawText);
}
