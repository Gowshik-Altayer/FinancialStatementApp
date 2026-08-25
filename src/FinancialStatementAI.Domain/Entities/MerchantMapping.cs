using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>A known merchant-name pattern -> Category mapping — the second rung of the hybrid
/// classification ladder (Rules -> Merchant Mapping -> Known Classification -> LLM, requirement
/// #17), and a genuinely extensible one: not a hardcoded switch statement, but data any admin
/// could add to without a code change (requirement #6).</summary>
public class MerchantMapping : BaseEntity
{
    public string MerchantPattern { get; set; } = string.Empty;
    public MerchantMatchType MatchType { get; set; } = MerchantMatchType.Contains;

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsSystemDefined { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
