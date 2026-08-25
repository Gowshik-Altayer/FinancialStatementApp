using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class StatementConfiguration : IEntityTypeConfiguration<Statement>
{
    public void Configure(EntityTypeBuilder<Statement> builder)
    {
        builder.Property(s => s.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(s => s.StoredFilePath).IsRequired().HasMaxLength(1000);
        builder.Property(s => s.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(s => s.DocumentType).IsRequired().HasConversion<string>().HasMaxLength(32);

        builder.Property(s => s.AccountHolderName).HasMaxLength(256);
        builder.Property(s => s.ProviderName).HasMaxLength(256);
        builder.Property(s => s.AccountNumberMasked).HasMaxLength(64);
        builder.Property(s => s.Currency).HasMaxLength(8);

        builder.Property(s => s.OpeningBalance).HasPrecision(18, 2);
        builder.Property(s => s.ClosingBalance).HasPrecision(18, 2);
        builder.Property(s => s.TotalDebits).HasPrecision(18, 2);
        builder.Property(s => s.TotalCredits).HasPrecision(18, 2);
        builder.Property(s => s.TotalPayments).HasPrecision(18, 2);
        builder.Property(s => s.TotalPurchases).HasPrecision(18, 2);

        builder.Property(s => s.ProcessingStatus).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(s => s.ProcessingStatus);
        builder.HasIndex(s => s.UserId);

        builder.HasMany(s => s.Transactions)
            .WithOne(t => t.Statement)
            .HasForeignKey(t => t.StatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.ProcessingJobs)
            .WithOne(j => j.Statement)
            .HasForeignKey(j => j.StatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.ProcessingErrors)
            .WithOne(e => e.Statement)
            .HasForeignKey(e => e.StatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.ReconciliationResults)
            .WithOne(r => r.Statement)
            .HasForeignKey(r => r.StatementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
