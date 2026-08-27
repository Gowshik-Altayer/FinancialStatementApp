using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class DashboardWidgetPreferenceConfiguration : IEntityTypeConfiguration<DashboardWidgetPreference>
{
    public void Configure(EntityTypeBuilder<DashboardWidgetPreference> builder)
    {
        builder.Property(p => p.WidgetKey).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Role).HasConversion<string>().HasMaxLength(32);

        // Exactly one of Role/UserId is set per row — a role-default row or a per-user override,
        // never both, never neither (see the entity's own doc comment).
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_DashboardWidgetPreferences_RoleXorUserId",
            "([Role] IS NOT NULL AND [UserId] IS NULL) OR ([Role] IS NULL AND [UserId] IS NOT NULL)"));

        // Filtered unique indexes (SQL Server allows multiple NULLs through a unique index, so
        // these only need to constrain the row shape they actually apply to).
        builder.HasIndex(p => new { p.Role, p.WidgetKey })
            .IsUnique()
            .HasFilter("[UserId] IS NULL");

        builder.HasIndex(p => new { p.UserId, p.WidgetKey })
            .IsUnique()
            .HasFilter("[Role] IS NULL");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
