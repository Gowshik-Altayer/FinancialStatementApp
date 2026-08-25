using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class ProcessingErrorConfiguration : IEntityTypeConfiguration<ProcessingError>
{
    public void Configure(EntityTypeBuilder<ProcessingError> builder)
    {
        builder.Property(e => e.Stage).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.ErrorMessage).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.ErrorDetails).HasMaxLength(2000);

        builder.HasIndex(e => e.StatementId);
    }
}
