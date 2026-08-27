using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class OcrTextBlockConfiguration : IEntityTypeConfiguration<OcrTextBlock>
{
    public void Configure(EntityTypeBuilder<OcrTextBlock> builder)
    {
        builder.Property(b => b.Text).IsRequired().HasMaxLength(2000);
        builder.Property(b => b.Confidence).HasPrecision(5, 4);

        builder.HasIndex(b => b.StatementExtractionId);
    }
}
