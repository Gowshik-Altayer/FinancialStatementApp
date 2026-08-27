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
            .Include(e => e.TextBlocks)
            .Include(e => e.TableRegions)
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
            existing.ConfidenceScore = extraction.ConfidenceScore;

            // A reprocess fully replaces the previous OCR detail rather than accumulating it,
            // the same rule as every other field on this row.
            dbContext.OcrTextBlocks.RemoveRange(existing.TextBlocks);
            dbContext.OcrTableRegions.RemoveRange(existing.TableRegions);
            foreach (var block in extraction.TextBlocks)
            {
                block.StatementExtractionId = existing.Id;
            }
            foreach (var table in extraction.TableRegions)
            {
                table.StatementExtractionId = existing.Id;
            }
            existing.TextBlocks = extraction.TextBlocks;
            existing.TableRegions = extraction.TableRegions;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
