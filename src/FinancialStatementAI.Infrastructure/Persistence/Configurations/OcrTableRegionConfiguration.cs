using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class OcrTableRegionConfiguration : IEntityTypeConfiguration<OcrTableRegion>
{
    public void Configure(EntityTypeBuilder<OcrTableRegion> builder)
    {
        builder.Property(t => t.Html).IsRequired();
        builder.Property(t => t.Confidence).HasPrecision(5, 4);

        builder.HasIndex(t => t.StatementExtractionId);
    }
}
