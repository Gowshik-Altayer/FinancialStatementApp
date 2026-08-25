using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class StatementExtractionRepository(AppDbContext dbContext) : IStatementExtractionRepository
{
    public async Task UpsertAsync(StatementExtraction extraction, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.StatementExtractions
            .SingleOrDefaultAsync(e => e.StatementId == extraction.StatementId, cancellationToken);

        if (existing is null)
        {
            dbContext.StatementExtractions.Add(extraction);
        }
        else
        {
            existing.ExtractionMethod = extraction.ExtractionMethod;
            existing.RawText = extraction.RawText;
            existing.PageCount = extraction.PageCount;
            existing.CharacterCount = extraction.CharacterCount;
            existing.HasUsableText = extraction.HasUsableText;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
