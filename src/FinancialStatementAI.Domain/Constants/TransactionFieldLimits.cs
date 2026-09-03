namespace FinancialStatementAI.Domain.Constants;

/// <summary>Column-length limits for Transaction's text fields — mirrored in
/// TransactionConfiguration's EF Fluent config (the actual DB constraint) and enforced again in
/// TransactionExtractionService before a ParsedTransaction is ever built, so a malformed or
/// runaway-concatenated source line (e.g. several unrecognized lines glued together as
/// "continuation of the previous description") degrades to a truncated-but-saved description
/// rather than crashing the whole statement's SaveChangesAsync with a SQL truncation error
/// (requirement #14: one bad transaction should never fail the whole statement).</summary>
public static class TransactionFieldLimits
{
    public const int DescriptionMaxLength = 1000;
    public const int MerchantMaxLength = 500;
}
