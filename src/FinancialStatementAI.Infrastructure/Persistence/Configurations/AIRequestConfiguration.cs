using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class AIRequestConfiguration : IEntityTypeConfiguration<AIRequest>
{
    public void Configure(EntityTypeBuilder<AIRequest> builder)
    {
        builder.Property(r => r.Provider).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Model).IsRequired().HasMaxLength(128);
        builder.Property(r => r.RequestType).IsRequired().HasMaxLength(64);
        builder.Property(r => r.ErrorMessage).HasMaxLength(1000);

        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => new { r.Provider, r.Model });

        builder.HasOne(r => r.Statement)
            .WithMany()
            .HasForeignKey(r => r.StatementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Transaction)
            .WithMany()
            .HasForeignKey(r => r.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
