using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialStatementAI.Infrastructure.Persistence.Configurations;

public class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        builder.Property(j => j.HangfireJobId).HasMaxLength(64);
        builder.Property(j => j.Stage).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(j => j.Status).IsRequired().HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(j => j.StatementId);
        builder.HasIndex(j => j.Status);

        builder.HasMany(j => j.ProcessingErrors)
            .WithOne(e => e.ProcessingJob)
            .HasForeignKey(e => e.ProcessingJobId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
