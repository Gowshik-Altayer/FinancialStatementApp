using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class AIUsageMetricConfiguration : IEntityTypeConfiguration<AIUsageMetric>
{
    public void Configure(EntityTypeBuilder<AIUsageMetric> builder)
    {
        builder.Property(m => m.Provider).IsRequired().HasMaxLength(64);
        builder.Property(m => m.Model).IsRequired().HasMaxLength(128);
        builder.Property(m => m.RequestType).IsRequired().HasMaxLength(64);

        builder.HasIndex(m => new { m.Date, m.Provider, m.Model, m.RequestType }).IsUnique();
    }
}
