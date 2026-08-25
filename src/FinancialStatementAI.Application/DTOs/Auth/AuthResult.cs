namespace FinancialStatementAI.Application.DTOs.Auth;

public class AuthResult
{
    public bool Succeeded { get; private init; }
    public string? Error { get; private init; }
    public AuthResponse? Response { get; private init; }

    public static AuthResult Success(AuthResponse response) => new() { Succeeded = true, Response = response };
    public static AuthResult Failure(string error) => new() { Succeeded = false, Error = error };
}
