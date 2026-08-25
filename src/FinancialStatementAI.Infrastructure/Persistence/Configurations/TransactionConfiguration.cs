using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.Property(t => t.Description).IsRequired().HasMaxLength(1000);
        builder.Property(t => t.Merchant).HasMaxLength(500);
        builder.Property(t => t.ReferenceNumber).HasMaxLength(128);
        builder.Property(t => t.Currency).HasMaxLength(8);
        builder.Property(t => t.PageSourceLocation).HasMaxLength(128);

        builder.Property(t => t.DebitAmount).HasPrecision(18, 2);
        builder.Property(t => t.CreditAmount).HasPrecision(18, 2);
        builder.Property(t => t.Amount).HasPrecision(18, 2);

        builder.Property(t => t.TransactionType).IsRequired().HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(t => t.StatementId);
        builder.HasIndex(t => t.CategoryId);
        builder.HasIndex(t => t.TransactionDate);
        builder.HasIndex(t => t.IsPotentialDuplicate);

        builder.HasOne(t => t.DuplicateOfTransaction)
            .WithMany()
            .HasForeignKey(t => t.DuplicateOfTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Extraction)
            .WithOne(e => e.Transaction)
            .HasForeignKey<TransactionExtraction>(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Classifications)
            .WithOne(c => c.Transaction)
            .HasForeignKey(c => c.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Corrections)
            .WithOne(c => c.Transaction)
            .HasForeignKey(c => c.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.ProcessingErrors)
            .WithOne(e => e.Transaction)
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
