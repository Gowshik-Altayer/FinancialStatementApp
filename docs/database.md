# Database

SQL Server database `FinancialStatementAI`, managed with EF Core Migrations. `AppDbContext`
(`src/FinancialStatementAI.Infrastructure/Persistence/AppDbContext.cs`) is the only place the
schema is defined — entity configurations live one-per-entity under
`Persistence/Configurations/` and are picked up automatically via
`modelBuilder.ApplyConfigurationsFromAssembly(...)`.

## Entity-relationship overview

```
User 1───* Statement 1───* Transaction 1───1 TransactionExtraction
                  │              │
                  │              ├──* TransactionClassification *───1 Category
                  │              ├──* TransactionCorrection ────────1 User (CorrectedByUser)
                  │              └──* ProcessingError
                  │
                  ├──* ProcessingJob ──* ProcessingError
                  └──* ReconciliationResult

AIRequest ──0..1── Statement
          ──0..1── Transaction

AIUsageMetric  (standalone daily rollup, no FKs)
Category ──* Transaction (CategoryId, current effective category)
```

## Why some history is append-only

Two tables are deliberately **append-only logs, not update-in-place rows**, because the
challenge explicitly requires preserving history rather than overwriting it:

- **TransactionClassification** — every classification attempt (rule, merchant mapping, or LLM)
  gets its own row with `ConfidenceScore`, `ClassificationMethod`, `Reason`, and `IsCurrent`.
  Reclassifying a transaction adds a new row and flips `IsCurrent`; it never edits the old one.
- **TransactionCorrection** — one row per human-edited field (`FieldName` + `OriginalValue` +
  `CorrectedValue` + `CorrectedByUserId` + `CorrectedAt`). Editing both Merchant and Category in
  one review produces two rows. `Transaction.CategoryId`/other live fields hold the *current*
  value; the correction rows are the audit trail of how it got there. See requirement #9.
- **ReconciliationResult** similarly keeps one row per reconciliation run (e.g. after a
  `POST /api/statements/{id}/reprocess`) rather than overwriting a single row, so a statement's
  reconciliation history is inspectable.

## Key design decisions

- **Money uses `decimal(18,2)`, confidence scores use `decimal(5,4)`** — never `float`/`double`
  (see requirement #9/#20; also `docs/architecture.md`). Configured explicitly via `HasPrecision`
  in each entity's configuration rather than relying on EF Core's convention default.
- **Enums are stored as strings** (`HasConversion<string>()`), not integers — this schema will be
  inspected directly in SSMS as part of evaluation, and a nullable/renumbered enum silently
  shifting integer values under a schema change is a real footgun; strings stay self-describing
  and stable across refactors at a small storage cost.
- **Nullability mirrors the challenge's "where available" language.** Almost every field on
  `Statement` (account holder, balances, statement period, etc.) and several on `Transaction`
  are nullable — extraction from a real-world statement will often come up short on some fields,
  and the schema has to represent "genuinely unknown" rather than forcing a placeholder value
  (see requirement #3: "handle incomplete or unavailable information gracefully").
- **`Transaction.DebitAmount`, `CreditAmount`, and `Amount` are three separate nullable columns**
  (not one signed amount), because the challenge explicitly lists all three as fields to capture
  — statements report them differently and normalization (Phase 9) needs the raw shape before
  collapsing to a single signed value for calculations.
- **Duplicate detection never deletes** (requirement #21): `Transaction.IsPotentialDuplicate` +
  `DuplicateOfTransactionId` flag a suspected duplicate without touching the original row.
- **Delete behavior** is deliberately mixed to avoid SQL Server's "multiple cascade paths"
  restriction while keeping cleanup sensible: `Statement → Transaction → (Extraction /
  Classification / Correction)` cascades all the way down (deleting a statement cleans up
  everything under it), but anything that could be reached from `Statement` by *two different
  paths* (e.g. `ProcessingError` via both `StatementId` and `TransactionId`/`ProcessingJobId`) is
  `Restrict` on the second path so only one path actually cascades. `Category → Transaction` is
  `SetNull` (deleting a category un-categorizes transactions instead of deleting them);
  `Category → TransactionClassification` is `Restrict` (classification history can't reference a
  category that no longer exists, so the category can't be deleted while history points to it).

## Seed data

`CategorySeeder` (`Persistence/Seed/CategorySeeder.cs`) idempotently inserts the 21 default
categories from `Domain.Constants.DefaultCategories` (Food & Dining, Groceries, Transportation,
… Other — the exact list from the challenge doc) marked `IsSystemDefined = true`. Categories
remain a normal editable/extensible entity (requirement #6) — the seeder only guarantees sensible
defaults exist; it never touches custom categories a user adds later. It runs automatically on
API startup in the Development environment only (see `Program.cs`), non-fatally — if SQL Server
isn't reachable yet, the API logs a warning and still starts rather than crashing.

## Running migrations

See the root [README.md](../README.md#database-migrations-from-phase-3-onward) for both the
`dotnet ef` CLI and Visual Studio Package Manager Console workflows. The `InitialCreate`
migration (`src/FinancialStatementAI.Infrastructure/Persistence/Migrations/`) creates all 12
tables described above.

> **Note on verification in this environment:** the sandbox this was built in has no running SQL
> Server engine (LocalDB is registered but its process fails to start here), so the migration was
> verified by successful `dotnet ef migrations add` scaffolding (which runs full EF Core model
> validation, including the cascade-path check described above) and a build of the generated
> migration — not by an actual `dotnet ef database update` against a live database. Run
> `Update-Database` / `dotnet ef database update` against a real SQL Server instance as the first
> verification step when picking this up in Visual Studio.
