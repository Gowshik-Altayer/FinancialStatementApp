using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class ReconciliationResultConfiguration : IEntityTypeConfiguration<ReconciliationResult>
{
    public void Configure(EntityTypeBuilder<ReconciliationResult> builder)
    {
        builder.Property(r => r.OpeningBalance).HasPrecision(18, 2);
        builder.Property(r => r.TotalCredits).HasPrecision(18, 2);
        builder.Property(r => r.TotalDebits).HasPrecision(18, 2);
        builder.Property(r => r.ExpectedClosingBalance).HasPrecision(18, 2);
        builder.Property(r => r.StatementClosingBalance).HasPrecision(18, 2);
        builder.Property(r => r.Discrepancy).HasPrecision(18, 2);

        builder.Property(r => r.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Notes).HasMaxLength(1000);

        builder.HasIndex(r => new { r.StatementId, r.CreatedAt });
    }
}
