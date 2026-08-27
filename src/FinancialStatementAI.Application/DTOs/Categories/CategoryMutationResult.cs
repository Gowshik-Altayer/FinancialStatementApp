namespace FinancialStatementAI.Application.DTOs.Categories;

public class CategoryMutationResult
{
    public bool Succeeded { get; private init; }
    public bool NotFound { get; private init; }
    public string? Error { get; private init; }
    public CategoryDetailResponse? Category { get; private init; }

    public static CategoryMutationResult Success(CategoryDetailResponse category) => new() { Succeeded = true, Category = category };
    public static CategoryMutationResult Failure(string error) => new() { Succeeded = false, Error = error };
    public static CategoryMutationResult AsNotFound() => new() { Succeeded = false, NotFound = true };
}
