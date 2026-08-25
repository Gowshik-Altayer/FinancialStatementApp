using System.Security.Cryptography;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Infrastructure.Security;

/// <summary>PBKDF2-HMAC-SHA256 password hashing. Stored format: iterations.saltBase64.hashBase64
/// — self-describing so the iteration count can be increased later without breaking existing
/// hashes (Verify reads whatever count was used to create the hash being checked).</summary>
public class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
