using Hangfire.Dashboard;

namespace FinancialStatementAI.Api;

/// <summary>Authorizes every request. This API authenticates via JWT bearer tokens, not cookies —
/// there's no practical way to gate a plain browser GET to the dashboard behind the same Bearer
/// scheme without a separate cookie-based login bridge, which is out of scope here. The dashboard
/// route is only mapped in the Development environment at all (see Program.cs); a real deployment
/// should put it behind IP allow-listing or a reverse-proxy-level auth gate instead of relying on
/// this filter, rather than this project pretending a JWT API can meaningfully protect it.</summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
