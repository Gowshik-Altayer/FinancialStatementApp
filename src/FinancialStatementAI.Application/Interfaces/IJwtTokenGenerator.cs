using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}
