namespace FinancialStatementAI.UnitTests;

public class SolutionWiringTests
{
    [Fact]
    public void Domain_Application_Infrastructure_Assemblies_Load_Correctly()
    {
        var domainAssembly = typeof(FinancialStatementAI.Domain.AssemblyMarker).Assembly;
        var applicationAssembly = typeof(FinancialStatementAI.Application.AssemblyMarker).Assembly;
        var infrastructureAssembly = typeof(FinancialStatementAI.Infrastructure.AssemblyMarker).Assembly;

        Assert.NotNull(domainAssembly);
        Assert.NotNull(applicationAssembly);
        Assert.NotNull(infrastructureAssembly);
    }
}
