using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class StatementExtractionConfiguration : IEntityTypeConfiguration<StatementExtraction>
{
    public void Configure(EntityTypeBuilder<StatementExtraction> builder)
    {
        builder.Property(e => e.ExtractionMethod).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RawText).IsRequired();
        builder.Property(e => e.ConfidenceScore).HasPrecision(5, 4);

        builder.HasIndex(e => e.StatementId).IsUnique();

        builder.HasOne(e => e.Statement)
            .WithOne(s => s.StatementExtraction)
            .HasForeignKey<StatementExtraction>(e => e.StatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.TextBlocks)
            .WithOne(b => b.StatementExtraction)
            .HasForeignKey(b => b.StatementExtractionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.TableRegions)
            .WithOne(t => t.StatementExtraction)
            .HasForeignKey(t => t.StatementExtractionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
