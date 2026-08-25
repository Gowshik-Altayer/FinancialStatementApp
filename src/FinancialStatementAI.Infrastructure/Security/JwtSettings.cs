namespace FinancialStatementAI.Infrastructure.Security;

/// <summary>Bound from the "Jwt" configuration section. SigningKey is a secret — see the root
/// README's Connection strings &amp; secrets section: never commit a real value, use User Secrets
/// or environment variables outside of local development.</summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
