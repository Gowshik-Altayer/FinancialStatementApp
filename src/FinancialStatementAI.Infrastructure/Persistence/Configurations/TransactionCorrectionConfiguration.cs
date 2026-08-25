using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class TransactionCorrectionConfiguration : IEntityTypeConfiguration<TransactionCorrection>
{
    public void Configure(EntityTypeBuilder<TransactionCorrection> builder)
    {
        builder.Property(c => c.FieldName).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.OriginalValue).HasMaxLength(1000);
        builder.Property(c => c.CorrectedValue).IsRequired().HasMaxLength(1000);
        builder.Property(c => c.CorrectionReason).HasMaxLength(1000);

        builder.HasIndex(c => c.TransactionId);

        builder.HasOne(c => c.CorrectedByUser)
            .WithMany()
            .HasForeignKey(c => c.CorrectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
