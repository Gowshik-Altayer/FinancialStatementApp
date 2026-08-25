using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class TransactionClassificationConfiguration : IEntityTypeConfiguration<TransactionClassification>
{
    public void Configure(EntityTypeBuilder<TransactionClassification> builder)
    {
        builder.Property(c => c.ConfidenceScore).IsRequired().HasPrecision(5, 4);
        builder.Property(c => c.ClassificationMethod).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.Reason).HasMaxLength(1000);

        builder.HasIndex(c => c.TransactionId);
        builder.HasIndex(c => new { c.TransactionId, c.IsCurrent });
    }
}
