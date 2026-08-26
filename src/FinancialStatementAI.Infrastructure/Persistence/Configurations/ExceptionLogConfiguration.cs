using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class ExceptionLogConfiguration : IEntityTypeConfiguration<ExceptionLog>
{
    public void Configure(EntityTypeBuilder<ExceptionLog> builder)
    {
        builder.Property(e => e.ExceptionType).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Message).IsRequired().HasMaxLength(2000);
        // StackTrace is intentionally left without a HasMaxLength (nvarchar(max)) — depth varies
        // too widely to bound safely without silently truncating the one detail an operator
        // needs most.
        builder.Property(e => e.RequestPath).HasMaxLength(500);
        builder.Property(e => e.RequestMethod).HasMaxLength(16);

        builder.HasIndex(e => e.OccurredAt);
    }
}
