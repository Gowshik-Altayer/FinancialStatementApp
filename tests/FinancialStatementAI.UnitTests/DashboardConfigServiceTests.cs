using FinancialStatementAI.Application.DTOs.Dashboard;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class DashboardConfigServiceTests
{
    private readonly Mock<IDashboardConfigRepository> _repository = new();
    private static readonly Guid UserId = Guid.NewGuid();

    private DashboardConfigService CreateService() => new(_repository.Object);

    [Fact]
    public async Task A_User_Override_Replaces_The_Role_Default_For_The_Same_Widget()
    {
        _repository.Setup(r => r.GetRoleDefaultsAsync(UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DashboardWidgetPreference { Role = UserRole.User, WidgetKey = "kpi-total-statements", IsVisible = true, SortOrder = 0 }]);
        _repository.Setup(r => r.GetUserOverridesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DashboardWidgetPreference { UserId = UserId, WidgetKey = "kpi-total-statements", IsVisible = false, SortOrder = 5 }]);

        var result = await CreateService().GetResolvedConfigAsync(UserId, UserRole.User);

        var widget = result.Single(w => w.WidgetKey == "kpi-total-statements");
        Assert.False(widget.IsVisible); // override wins
        Assert.Equal(5, widget.SortOrder);
        Assert.Equal("UserOverride", widget.Source);
    }

    [Fact]
    public async Task A_Widget_With_No_Row_In_Either_Layer_Defaults_To_Visible_And_Sorts_Last()
    {
        _repository.Setup(r => r.GetRoleDefaultsAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetUserOverridesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateService().GetResolvedConfigAsync(UserId, UserRole.User);

        // Every widget in the registry appears, all visible, all "SystemDefault" (no seed data at all).
        Assert.Equal(DashboardWidgetKeys.All.Count, result.Count);
        Assert.All(result, w => Assert.True(w.IsVisible));
        Assert.All(result, w => Assert.Equal("SystemDefault", w.Source));
    }

    [Fact]
    public async Task Resolved_Config_Always_Covers_The_Full_Widget_Registry_Even_With_Partial_Seed_Data()
    {
        _repository.Setup(r => r.GetRoleDefaultsAsync(UserRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DashboardWidgetPreference { Role = UserRole.Admin, WidgetKey = DashboardWidgetKeys.KpiTotalStatements, IsVisible = true, SortOrder = 0 }]);
        _repository.Setup(r => r.GetUserOverridesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateService().GetResolvedConfigAsync(UserId, UserRole.Admin);

        Assert.Equal(DashboardWidgetKeys.All.Count, result.Count);
    }

    [Fact]
    public async Task Replacing_User_Overrides_Never_Touches_Role_Defaults()
    {
        var request = new UpdateDashboardWidgetPreferencesRequest
        {
            Items = [new WidgetPreferenceItem { WidgetKey = "kpi-total-statements", IsVisible = false, SortOrder = 0 }]
        };

        await CreateService().ReplaceUserOverridesAsync(UserId, request);

        _repository.Verify(r => r.ReplaceUserOverridesAsync(UserId, It.Is<IReadOnlyList<DashboardWidgetPreference>>(items => items.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.ReplaceRoleDefaultsAsync(It.IsAny<UserRole>(), It.IsAny<IReadOnlyList<DashboardWidgetPreference>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
