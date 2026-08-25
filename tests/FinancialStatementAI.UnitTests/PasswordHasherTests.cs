using FinancialStatementAI.Infrastructure.Security;

namespace FinancialStatementAI.UnitTests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_Returns_True_For_The_Correct_Password()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_Returns_False_For_The_Wrong_Password()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("wrong password", hash));
    }

    [Fact]
    public void Hash_Produces_A_Different_Value_Each_Time_Due_To_Random_Salt()
    {
        var hash1 = _hasher.Hash("same password");
        var hash2 = _hasher.Hash("same password");

        Assert.NotEqual(hash1, hash2);
    }
}
