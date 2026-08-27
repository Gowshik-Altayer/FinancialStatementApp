namespace FinancialStatementAI.Application.DTOs.Categories;

public class CategoryDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemDefined { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>Per-category usage stats for the Categories page (requirement 10) — scoped to the
/// current user's own transactions. "AI classified %" / "human corrected %" is derived from
/// whether a human has ever recorded a correction on a transaction currently in this category,
/// not a separately-tracked flag — there's no other honest signal for "did a human touch this."</summary>
public class CategoryStatsResponse
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AiClassifiedPercent { get; set; }
    public decimal HumanCorrectedPercent { get; set; }
}
