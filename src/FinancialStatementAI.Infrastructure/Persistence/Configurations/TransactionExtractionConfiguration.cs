using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class TransactionExtractionConfiguration : IEntityTypeConfiguration<TransactionExtraction>
{
    public void Configure(EntityTypeBuilder<TransactionExtraction> builder)
    {
        builder.Property(e => e.RawText).IsRequired().HasMaxLength(4000);
        builder.Property(e => e.ExtractionMethod).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.ConfidenceScore).HasPrecision(5, 4);

        builder.HasIndex(e => e.TransactionId).IsUnique();
    }
}
