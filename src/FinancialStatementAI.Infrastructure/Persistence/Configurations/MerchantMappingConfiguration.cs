using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class MerchantMappingConfiguration : IEntityTypeConfiguration<MerchantMapping>
{
    public void Configure(EntityTypeBuilder<MerchantMapping> builder)
    {
        builder.Property(m => m.MerchantPattern).IsRequired().HasMaxLength(256);
        builder.Property(m => m.MatchType).IsRequired().HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(m => m.MerchantPattern);

        builder.HasOne(m => m.Category)
            .WithMany()
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
