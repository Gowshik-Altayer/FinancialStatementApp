# AI / Document Processing

> Built up phase by phase. This revision covers **Phase 7 (direct PDF text extraction and the
> OCR-vs-direct decision)**, **Phase 8 (OCR / Document Intelligence abstractions)**,
> **Phase 9 (transaction extraction and normalization)**, and **Phase 10 (AI classification)**.
> Reconciliation (Phase 11) will extend this document as it lands.

## How we determine whether OCR is required

This is the first decision in the challenge's document-processing pipeline (requirement #2), and
it's made once per statement, in `StatementProcessingService.ExtractPdfTextAsync` /
`PdfTextExtractionService.Extract`:

1. If the uploaded file isn't a PDF (JPG/PNG), there's no text layer to even attempt — it's
   always routed to OCR/Vision (Phase 8). No decision needed.
2. If it is a PDF, `PdfTextExtractionService` opens it with PdfPig and pulls the text layer
   directly, page by page — **no OCR, no AI call, no network round-trip**. This is the "direct
   extraction" path from the architecture diagram in the challenge doc, and it's essentially
   free (single-digit milliseconds for a typical statement) compared to OCR or an LLM call, so we
   always try it first regardless of file size.
3. We count non-whitespace characters across all pages and compute the **average per page**.
   If that average is at least `TextExtractionThresholds.MinUsableCharactersPerPage` (20), the
   text is considered **usable** and the statement moves to `ExtractionComplete` — Phase 9 will
   parse transactions straight out of this raw text.
4. If it's below that threshold, the PDF is judged to have **no meaningful embedded text layer**
   — almost always because it's a scanned image wrapped in a PDF container (a real digital
   statement from a bank has thousands of characters of embedded text; a blank or near-blank
   extraction result is the signature of "this is actually a picture, not text"). The statement
   stays at `Processing`, flagged as needing OCR/Vision once Phase 8 exists to provide it.

### Why "characters per page" and not "characters total" or "pages with any text"

A single-page statement with 15 characters (e.g. stray watermark text PdfPig can still pull off a
scanned image) and a 20-page statement with 15 characters *total* are both clearly unusable, but
"total characters" alone would need a much higher, page-count-dependent threshold to catch both
cases correctly. Normalizing to a **per-page average** makes the same threshold work regardless of
how many pages the statement has, and is cheap to compute from information we already have.

### Why 20 characters per page, specifically

It's deliberately a very low bar. The goal isn't to guarantee the text is *good enough to parse
transactions from* (that's a separate, harder judgment Phase 9's normalization step makes) — it's
only to distinguish "there is essentially no text here" (a scanned page) from "there is
text here" (a digital PDF, however messy its layout). A real bank/credit-card statement page,
even a poorly-formatted one, produces text in the hundreds to thousands of characters; the
failure mode this threshold guards against is a PDF that's just a raster image with maybe a
handful of stray characters from a watermark or page number. Making it configurable
(`Domain.Constants.TextExtractionThresholds`) means it can be tuned later against real-world
statement samples without touching the decision logic itself.

## Why PdfPig

Pure .NET, MIT-licensed, no native/unmanaged dependencies (important for a straightforward
`dotnet build`/Docker story later). It's also already used for a different purpose in Phase 6
(opening a PDF to check it isn't corrupted/password-protected during upload validation) — Phase 7
reuses the exact same library for its primary purpose, text extraction, rather than introducing a
second PDF library for a closely related job.

## What's persisted, and why as its own entity

`StatementExtraction` (one row per statement, 1:1) holds the full raw extracted text
(`RawText`), page count, character count, and the `HasUsableText` verdict. It's deliberately
separate from `TransactionExtraction` (per-transaction, only populated once Phase 9 actually
parses transactions out of this raw text) — `StatementExtraction` is the document-level "what did
we get off the page" record that both feeds Phase 9 and, on its own, already answers "does this
statement need OCR" for the UI (the Statement Detail screen's "Text extraction" card) without
needing anything downstream to exist yet.

## OCR and Document Intelligence (Phase 8)

Two abstractions, per requirements #13/#14 — the business layer never depends on an Azure SDK
class directly, only on `IOcrService` / `IDocumentIntelligenceService`:

| Interface | Real implementation | Purpose |
|---|---|---|
| `IOcrService` | `AzureOcrService` (Azure AI Vision, Read feature) | Convert an image or scanned PDF page into plain text |
| `IDocumentIntelligenceService` | `AzureDocumentIntelligenceService` (`prebuilt-document` model) | Pull structured fields/layout out of a document |

Both default to a **Mock** implementation (`MockOcrService`, `MockDocumentIntelligenceService`) —
selected via `Ocr:Provider` / `DocumentIntelligence:Provider` config, same pattern as Phase 6's
`FileStorage:Provider` switch. This isn't a shortcut: it's the only way to make the OCR branch of
the pipeline demoable and testable without an Azure subscription, and the challenge explicitly
allows Mock implementations as a valid deliverable. Mock output is unambiguously labeled
(`"[MOCK OCR OUTPUT - simulated for local development, not a real OCR result]"`) so it's never
mistaken for genuine extracted data — this matters given requirement #16's hallucination-
prevention principle: even *simulated* data has to be honest about not being real.

**Where OCR sits in the pipeline** (`StatementProcessingService`): PDFs try direct extraction
(Phase 7) first; only when that finds no usable text does OCR run. Images skip straight to OCR
since they have no text layer to try extracting directly at all. If OCR *also* fails to produce
usable text, the statement is marked `ExtractionFailed` — both extraction paths have now been
exhausted.

**Why Document Intelligence isn't on the critical path yet**: it extracts structured
fields/tables, which is genuinely valuable for well-known statement layouts, but building that
against Mock output wouldn't demonstrate anything beyond "the interface compiles" — a bank
statement's *transaction table* is exactly what Phase 9's own parsing logic is responsible for
extracting from the raw text OCR/direct-extraction already produced. The abstraction exists,
wired into DI and ready to use (e.g. for pulling `AccountNumber`/`StatementDate` fields more
reliably from a known layout), but isn't yet called from the processing pipeline — a defensible,
documented scope boundary rather than a silent gap.

## Transaction extraction and normalization (Phase 9)

### Why rule-based, not LLM-based

`TransactionExtractionService` is a deterministic regex/rule parser, not an LLM call. This is a
deliberate choice, not a shortcut: extracting an exact date and an exact amount is precisely the
kind of task where an LLM's failure mode (a plausible-looking but wrong number) is most
dangerous — requirement #16 draws a hard line that dates, amounts, and reference numbers must
never be invented or "corrected" by AI, only read verbatim from the source. A rule-based parser
either finds the value in the text or doesn't; it cannot hallucinate a value that isn't there.
LLM/AI involvement is reserved for Phase 10's classification, where interpreting what a merchant
name *means* (judgment) is fundamentally different from reading what a date or amount *is*
(extraction).

### How transaction rows are identified

For each line of the raw extracted text (from Phase 7/8):
1. Try to match a date at the start of the line, trying three shapes in order: `MM/DD[/YYYY]` or
   `MM-DD[-YYYY]`, `DD-Mon` (e.g. `01-Aug`), and `Mon DD` (e.g. `Aug 01`) — covering all three
   example formats in the challenge doc plus the with-year variants.
2. If a date matched, look for a trailing amount token in the rest of the line — one requiring
   **exactly two decimal places** (`\.\d{2}`). This is deliberate: without it, a bare reference
   number elsewhere on the line (e.g. `REF 123456`) would be mistaken for an amount. The
   documented tradeoff is that a statement showing whole-dollar amounts with no decimal point
   wouldn't be recognized — not handled by real-world statement conventions, which reliably
   include cents.
3. If both a date and an amount are found, that line is a transaction. If a line has neither
   (e.g. a header, a footer, "Thank you for banking with us"), it's silently skipped — one
   unparseable line never fails the whole statement (requirement #14).
4. A line with **no** leading date is treated as a **continuation of the previous transaction's
   description** — this is how wrapped/multi-line descriptions (requirement #5) are handled: the
   overflow line has no date or amount of its own, so it naturally falls into this branch.

### Normalizing dates, amounts, and direction

- **Dates**: since most statement lines omit the year (`01/08`, not `01/08/2026`), a reference
  year is threaded through from `IStatementFieldExtractionService`'s best guess at the statement
  period (falling back to the current year if that wasn't found either — see the limitation
  noted below).
- **Amounts**: currency symbols (`$`/`€`/`£`), thousands separators (`,`), and parentheses are
  all stripped/interpreted before parsing; a symbol also sets the transaction's `Currency`.
- **Direction (Debit/Credit/Payment/Transfer/etc.)**: checked in priority order — an explicit
  pipe-delimited `Debit`/`Credit` segment (the challenge's second example format) wins outright;
  otherwise explicit keywords in the line (`credit`, `refund`, `transfer`, `payment`, `debit`,
  `purchase`) are checked next; only if none of those match does it fall back to the bare sign
  (negative/parenthesized/`DR`-suffixed → Debit, everything else → Credit). This ordering matters
  because bank and credit-card statements don't agree on sign convention (a credit card shows
  purchases as positive, a checking account shows them as negative) — keywords are a more
  reliable signal than sign alone whenever they're present.

### Known limitation: statement period/year detection is best-effort

`StatementFieldExtractionService`'s label-driven regex approach (`"Opening Balance $1,000.00"`,
etc.) does not yet parse date **ranges** (`"Statement Period: 01/01/2026 - 01/31/2026"`) — only
single labeled amounts and short text fields. When the statement period can't be determined, the
transaction parser's reference year falls back to the current calendar year, which is wrong for
statements from a different year. This is flagged rather than silently accepted: a natural
follow-up would extend `StatementFieldExtractionService` with a date-range pattern once real
statement samples are available to test it against.

### Duplicate detection (requirement #21)

`TransactionRepository.ReplaceForStatementAsync` does two things in one pass: it replaces
whatever transactions this *same* statement had from a prior parse (so reprocessing doesn't
accumulate duplicates of its own previous attempt), and it flags — never deletes — any new
transaction that matches one already belonging to a *different* statement of the same user on
`{TransactionDate, Amount, Merchant}`. A flagged transaction still gets saved with
`IsPotentialDuplicate = true` and `DuplicateOfTransactionId` pointing at the original, surfaced
for human review (Phase 12) rather than silently dropped.

## AI classification (Phase 10)

### The hybrid ladder, in order, and why each rung exists before the next

`TransactionClassificationService` tries four rungs in order and stops at the first confident
match, per requirement #17:

1. **Rules** (`Domain.Constants.ClassificationKeywordRules`) — structural keywords checked
   against the transaction's **description** (not the merchant field — see "a subtle bug" below):
   `"PAYROLL"` → Payroll, `"RENT PAYMENT"` → Rent, `"OVERDRAFT FEE"` → Bank Fee, etc. These are
   checked *first*, ahead of merchant matching, because they're more reliable than any merchant
   name pattern could be for this class of transaction — a payroll deposit is Payroll no matter
   which bank's payroll system issued it.
2. **Merchant Mapping** (`Domain.Entities.MerchantMapping`, seeded from
   `DefaultMerchantMappings` — the exact examples from the challenge doc: `"UBER"` →
   Transportation, `"WHOLE FOODS"` → Groceries, `"AWS"` → Software & SaaS, `"DELTA AIR"` →
   Travel — plus other common, unambiguous merchants). A genuinely extensible table (requirement
   #6), not a hardcoded switch statement — any admin could add a row without a code change.
3. **Known Classification** — has a human already corrected *this exact merchant* to a specific
   category before (for this user)? `IClassificationHistoryRepository` looks at
   `TransactionCorrection` rows with `FieldName = Category`. This is literally how a human
   correction improves future classification (requirement #9's reasoning question #10) — no
   retraining involved, just checking whether the answer is already known.
4. **LLM** (`ITransactionClassifier`) — only reached when none of the above matched. Requirement
   #46 ("don't send every transaction to the LLM") is enforced structurally: the LLM is
   physically the last thing tried, not a policy that has to be remembered.

### A subtle bug this ladder's design caught (and fixed) during testing

The keyword-rule check was originally written against `transaction.Merchant ?? transaction
.Description` — the same "primary text" used for merchant mapping and the LLM. A unit test using
a transaction with `Merchant = "SOME BANK"` and `Description = "PAYROLL DEPOSIT FROM EMPLOYER"`
exposed that this missed the rule entirely (it checked "SOME BANK" for "PAYROLL", not the
description). In production this can't happen today — Phase 9's parser always sets
`Merchant = Description` — but it's a real latent bug for whenever merchant-name cleanup
diverges the two fields, so the rule check was changed to test `transaction.Description`
specifically, which is where structural keywords actually live.

### Never trusting the LLM's output (requirement #15)

`TransactionClassificationService` validates the category name the LLM returns against the
actual seeded category list before accepting it. If the LLM invents a category that doesn't
exist (tested explicitly — see `An_Invalid_Category_From_The_Llm_Is_Never_Trusted_And_Falls_Back
_To_Other`), the transaction is reassigned to `Other` and the confidence is force-capped just
under the "review recommended" threshold, so it's guaranteed to surface for human review rather
than silently keeping a wrong result.

### Confidence thresholds

`Domain.Constants.ClassificationConfidenceThresholds` mirrors the challenge's own example
exactly: `>= 0.80` high confidence, `0.60–0.79` review recommended, `< 0.60` review required.
Each rung's confidence reflects how much it should be trusted: Rules and Known Classification are
`0.95` (a keyword match or a human's own prior correction are about as certain as this system
gets), Merchant Mapping is `0.90`, and the LLM's confidence is whatever it reports (validated,
never blindly inflated).

### `MockTransactionClassifier` is deliberately honest, not falsely confident

With no real LLM configured (the default), classifying a merchant none of the first three rungs
recognized always returns `Other` at `0.50` confidence — landing squarely in "review required."
This mirrors the same design decision as Phase 8's Mock OCR/Document Intelligence services: a
confident-looking wrong guess is worse than an honest "we don't know." Set
`Classification:Provider` to `OpenAI` or `AzureOpenAI` (plus the matching endpoint/key via User
Secrets) for real LLM classification — `OpenAiTransactionClassifier` and
`AzureOpenAiTransactionClassifier` share one prompt/JSON-parsing implementation
(`ChatCompletionClassifierCore`), since Azure OpenAI's 2.x SDK generation exposes the same
`ChatClient` type as the plain OpenAI client and only differs in how that client is constructed.

### AI cost tracking (requirement #46)

Every LLM call — success or failure — is logged as one `AIRequest` row (provider, duration,
success/failure) *only* when the LLM is actually reached; Rules/Merchant Mapping/Known
Classification hits never touch this table, since they never called an LLM. This is the concrete
mechanism behind "don't send every transaction to the LLM, and track what you do send."

### Known limitation: reprocessing does not yet preserve classification/correction history

`ITransactionRepository.ReplaceForStatementAsync` (Phase 9) deletes and recreates a statement's
transactions wholesale on every reprocess, rather than updating matching rows in place. That
means today, reprocessing a statement that already has human corrections or prior classifications
would discard them along with the recreated `Transaction` rows (cascade-deleted with them) —
tested and documented rather than silently accepted (see
`Reprocessing_Yields_One_Transaction_With_One_Current_Classification`). Phase 12 (human review)
will need to address this — most likely by matching parsed transactions against existing ones on
a natural key (date + amount + description) and updating in place instead of replacing, once
corrections exist to actually preserve.

## Trigger: synchronous today, background job from Phase 14

`POST /api/statements/{id}/reprocess` runs `StatementProcessingService.ProcessAsync` and blocks
until it's done — practical to build and test the extraction logic against before Hangfire exists,
and fast enough (single-digit milliseconds per statement, no external calls) that "runs
synchronously" doesn't currently violate the spirit of requirement #11 (don't do *long-running*
OCR/AI work in a request). Once Phase 14 wires up Hangfire, the pending `ProcessingJob` row that
upload already creates (Phase 6) is what actually gets consumed, and this endpoint's contract
changes to "enqueue and return 202 Accepted" without changing its URL or request/response shape.
