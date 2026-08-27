# AI / Document Processing

> Built up phase by phase. This revision covers **Phase 7 (direct PDF text extraction and the
> OCR-vs-direct decision)**, **Phase 8 (OCR / Document Intelligence abstractions)**,
> **Phase 9 (transaction extraction and normalization)**, **Phase 10 (AI classification)**,
> **Phase 11 (deterministic reconciliation)**, and **Phase 12 (human review + audit trail)**.

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

## OCR and Document Intelligence (Phase 8, real engine added later)

Two abstractions, per requirements #13/#14 — the business layer never depends on a concrete OCR
SDK directly, only on `IOcrService` / `IDocumentIntelligenceService`:

| Interface | Default implementation | Purpose |
|---|---|---|
| `IOcrService` | `PaddleOcrService` (PaddleOCR PP-OCRv6, via `ocr-service/`) | Convert an image or scanned PDF page into text, with per-block confidence and bounding boxes |
| `IDocumentIntelligenceService` | `MockDocumentIntelligenceService` (opt into `PaddleDocumentStructureService`/PP-StructureV3 via config) | Pull structured layout/tables out of a document |

`AzureOcrService`/`AzureDocumentIntelligenceService` remain as opt-in alternatives (`Ocr:Provider`
/ `DocumentIntelligence:Provider` = `"Azure"`), but neither is the default — requirement #16 rules
out relying on a paid/cloud engine as this project's primary OCR path.

### Why PaddleOCR over Tesseract or Surya

All three are genuinely open-source and runnable with no per-call cost, which is why each was
considered. PaddleOCR (PP-OCRv6 for detection/recognition, PP-StructureV3 for layout/table
reconstruction) was chosen because:

- **Tesseract** has no table-structure model at all — it returns a flat text stream (or, with
  `--psm` tuning, word-level boxes) but never reconstructs *which cells belong to which row/column*.
  A financial statement's transaction table is exactly the structure that matters most here, and
  Tesseract simply has nothing in this category — it would need a separate, hand-rolled
  table-reconstruction heuristic on top, which is precisely the "reinventing a worse version of an
  existing model" this project is trying to avoid.
- **Surya** is newer and improving fast, but as of this writing it's a younger project without
  PaddleOCR's multi-year production track record, and its table-recognition model lineage isn't as
  mature or as widely deployed as PP-StructureV3's. For a "production-ready" deliverable, betting
  the primary OCR engine on the less battle-tested option is the wrong trade.
- **PaddleOCR** ships PP-OCRv6 (fast, accurate general text detection/recognition, including
  rotated and dense text — common in scanned statements) *and* PP-StructureV3 (document layout
  analysis plus table-structure reconstruction to HTML) as one coherent, actively maintained
  toolkit. That combination — not just "an OCR model" but "an OCR model with a matching
  table-structure model from the same lineage" — is the specific fit this project's requirements
  (tables, transactions, columns) call for.

### Why OCR runs as a separate microservice, not in-process

PaddleOCR is built on PaddlePaddle, a Python-only ML framework — there is no native .NET port. The
Application layer still only ever talks to `IOcrService`/`IDocumentIntelligenceService`; those
interfaces are implemented in `FinancialStatementAI.Infrastructure/OCR/PaddleOcr/` as thin HTTP
clients calling a standalone FastAPI service in `ocr-service/` (see its own `README.md`). This
keeps .NET business logic (Application/Domain) with zero Python dependency, matches this project's
existing provider-switch pattern (`FileStorage:Provider`, `Classification:Provider`, etc.), and
means another OCR engine — including a future native option — can be added later purely by adding
another `IOcrService` implementation, with no change to `StatementProcessingService`.

`ocr-service/` exposes two endpoints:

| Endpoint | Model | Returns |
|---|---|---|
| `POST /ocr` | PP-OCRv6 | Per-page recognized text, overall confidence, and per-block text/confidence/bounding box |
| `POST /structure` | PP-StructureV3 | Reconstructed table regions (as HTML), per-table confidence, and bounding box |

Both accept a PDF or image upload; PDFs are rasterized to page images with `pypdfium2` before
being handed to PaddleOCR (PaddleOCR itself operates on images).

**Where OCR sits in the pipeline** (`StatementProcessingService`): PDFs try direct extraction
(Phase 7) first; only when that finds no usable text does OCR run. Images skip straight to OCR
since they have no text layer to try extracting directly at all. If OCR *also* fails to produce
usable text, the statement is marked `ExtractionFailed` — both extraction paths have now been
exhausted. When OCR *does* run and produce usable text, the pipeline additionally calls
`IDocumentIntelligenceService.AnalyzeAsync` (PP-StructureV3, when configured) to reconstruct table
regions — a scanned page is exactly the case where table structure is otherwise lost, since direct
PDF text extraction already preserves reading order without needing it. A failure here never fails
the reprocess: table regions are enrichment, not a gate.

**What gets persisted**: `StatementExtraction` now also carries a nullable `ConfidenceScore`
(PP-OCRv6's overall confidence for the extraction) alongside two new child collections —
`OcrTextBlock` (one row per detected text region: page, text, confidence, bounding box) and
`OcrTableRegion` (one row per reconstructed table: page, HTML, confidence, bounding box). Both are
optional/best-effort detail on top of the same `RawText`/`HasUsableText` fields Phase 7/8
originally introduced — nothing downstream requires them to be present, since not every
`IOcrService`/`IDocumentIntelligenceService` implementation populates them (`AzureOcrService`
doesn't, for instance).

**Verified against a real run**: `ocr-service/` has since been run for real (paddleocr==3.7.0,
paddlepaddle==3.3.1) against `sample-data/scanned-bank-statement.png` — see `ocr-service/README.md`
for the three real bugs that surfaced and were fixed (a oneDNN/PaddlePaddle incompatibility
requiring `enable_mkldnn=False`, a result-shape mismatch, and a missing `paddlex[ocr]` dependency
for PP-StructureV3), plus an important caveat: PP-StructureV3 loads roughly a dozen models at once
and was observed to crash the whole process (no Python traceback — an OS-level OOM kill or native
crash) on repeated use on a memory-constrained CPU-only machine. `StatementProcessingService`
already treats a failed/unreachable structure call as non-fatal (see "Where OCR sits in the
pipeline" above), so this degrades gracefully to OCR-without-tables rather than failing the
reprocess — but it's worth knowing about before relying on PP-StructureV3 in a resource-constrained
environment.

**Why table structure feeds into transaction parsing, not just storage**: OCR'd plain text
routinely puts every table cell on its own line — PP-OCRv6 detects and reads text region by
region, not row by row, so a scanned statement's raw text looks like `"03/02\nPAYROLL DIRECT
DEPOSIT\nDD10029\n2,300.00\n..."` rather than one line per transaction. Phase 9's line-based
`TransactionExtractionService.Extract` (built for the OCR case being a coherent read like Azure
Vision's) requires a date and an amount on the same line, so it silently finds zero transactions
against this shape — confirmed against a real end-to-end run. `ExtractFromTable` parses
PP-StructureV3's reconstructed `<table>` HTML instead: each `<tr>` is one candidate transaction,
with the first date-shaped cell as the date, the last amount-shaped cell as the amount, and
everything else as the description. `StatementProcessingService.ExtractTransactions` prefers this
whenever a table region was found, falling back to the line-based parser otherwise (which is what
direct PDF text extraction — never OCR'd, never cell-per-line — always uses).

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

### Resolved (Phase 12): reprocessing now preserves classification/correction history

Phase 9's original `ReplaceForStatementAsync` deleted and recreated a statement's transactions
wholesale on every reprocess, which cascade-deleted any human corrections or classification
history along with the recreated `Transaction` rows — a known, documented limitation at the time.
Phase 12 fixes this exactly as previously planned: reparsed lines are matched against the
statement's own existing transactions by natural key (date + amount + description) and updated in
place — same `Transaction.Id`, so its `TransactionCorrection`/`TransactionClassification` rows are
untouched. `ApplyReparsedFields` deliberately never touches `CategoryId` (classification runs as
its own step right after), so a human's prior correction can't be reset by a bare re-extraction in
between. See `TransactionRepository.ReplaceForStatementAsync` and
`Reprocessing_Preserves_The_Same_Transaction_And_Accumulates_Classification_History` (the test that
replaced `Reprocessing_Yields_One_Transaction_With_One_Current_Classification`, which asserted the
old, now-fixed behavior).

## Deterministic reconciliation (Phase 11)

### Why this step has zero AI involvement

Reconciliation is pure arithmetic, not interpretation — there is nothing here for an LLM to add
except hallucination risk (requirement #16). `ReconciliationService.ReconcileAsync` computes:

```
Opening Balance + Total Credits − Total Debits = Expected Closing Balance
```

using the statement's own parsed transactions (`Amount > 0` sums into credits, `Amount < 0` sums
into debits, by absolute value), then compares that to the statement's *reported* Closing Balance
(from `StatementFieldExtractionService`, Phase 7) within a fixed `0.01` tolerance to absorb
rounding noise, not to paper over real discrepancies.

### Three outcomes, not two

A binary "matched / didn't match" would silently misrepresent statements this system simply
doesn't have enough information about, which is its own kind of dishonesty (the same principle
behind `MockOcrService`'s low-confidence-not-false-confidence design in Phase 8):

- **`Reconciled`** — expected and reported closing balances agree within tolerance.
- **`Mismatch`** — both balances are known and they genuinely disagree; `Discrepancy` (expected −
  reported) and a human-readable note are recorded so a reviewer isn't just told "no."
- **`InsufficientInformation`** — the statement is missing its Opening or Closing Balance (label
  wasn't found on the page, e.g. an unusual layout). No expected balance is guessed in this case;
  `ExpectedClosingBalance`/`Discrepancy` stay `null` rather than being computed from an assumed
  value.

### One row per run, not an update-in-place

Every `ReconcileAsync` call persists a *new* `ReconciliationResult` row via
`IReconciliationRepository.AddAsync` rather than overwriting the previous one, so a statement's
reconciliation history across multiple reprocess runs stays inspectable (mirrors
`docs/database.md`'s note that `ReconciliationResult` is an append-only history, not
current-state-only). `GetReconciliation` (statement detail/list surfaces) and the
`GET /api/statements/{id}/reconciliation` endpoint both read only the *latest* row
(`GetLatestAsync`, ordered by `CreatedAt` descending).

### Where it sits in the pipeline

`StatementProcessingService.ProcessAsync` runs reconciliation immediately after classification
completes, then marks the statement `PendingReview` — reconciliation output (deterministic, no AI
judgment calls) is exactly the kind of signal a human reviewer should see *before* deciding whether
to trust the AI-classified categories underneath it.

### A found-and-fixed bug this phase's tests surfaced

Writing `ReconciliationIntegrationTests` (a real four-digit balance, `$1000.00`, with no comma
thousands-separator) exposed that both amount-matching regexes in
`Infrastructure/Documents` — `StatementFieldExtractionService.AmountAfterLabel` and
`TransactionExtractionService.TrailingAmountRegex` — used a leading `\d{1,3}` digit-group cap
(intended only to bound the *comma-grouped* case, e.g. `1,234.56`). For a plain ungrouped number
of four or more digits, that cap forced the regex engine to backtrack and match starting mid-number
(e.g. matching `"000.00"` out of `"1000.00"`, silently parsing it as `0.00`) instead of failing to
match at all — a genuine, previously-untested correctness bug for any real statement whose amounts
are written without thousands separators. Fixed by relaxing both patterns' leading group to `\d+`,
which is a strict superset of what `\d{1,3}` could already match, so no prior passing case (e.g.
`129.45`, `1,234.56`) regressed.

## Human review and audit trail (Phase 12)

### Category correction only, deliberately

`TransactionCorrection` (Phase 1) is a generic per-field audit row — `CorrectedField` supports
seven fields — but the Phase 12 API (`POST /api/transactions/{id}/corrections`) only accepts
`Category`. Two reasons: Amount/date corrections would need to also re-trigger reconciliation
(new design surface this phase doesn't need), and — more subtly — a live Merchant/Description
correction would conflict with `ApplyReparsedFields` unconditionally refreshing those fields from
the raw text on every reprocess, silently reverting the correction exactly the way the pre-fix
`ReplaceForStatementAsync` used to revert categories. Rather than solve that now for fields the
challenge doesn't actually need corrected, the scope stays at Category — the field the review
workflow is actually about — and the limitation is written down rather than guessed around.

### Why a category correction survives on its own, with no special-casing

Once the corrected `Transaction` row survives a reprocess (the fix above), no extra code is needed
to make the correction "stick": `TransactionClassificationService`'s existing "Known
Classification" rung (`IClassificationHistoryRepository.FindPreviousCorrectedCategoryAsync`,
Phase 10) already looks up the most recent human correction for a transaction's merchant text
before ever reaching the LLM. Reclassification after a reprocess finds that same correction row
again (same transaction, same merchant, correction row never deleted) and reapplies it with
`ClassificationMethod.PreviousCorrection` at `0.95` confidence — the correction and the
self-healing classification ladder were designed independently (Phases 9/10 and 12) but compose
correctly once the identity-preservation bug is fixed.

### The review queue and "review priority" are computed, never stored

`GET /api/transactions/review-queue` and `GET /api/statements/{id}/transactions` both derive a
`reviewPriority` (`HighConfidence` / `ReviewRecommended` / `ReviewRequired`) from the transaction's
current `ConfidenceScore` against `ClassificationConfidenceThresholds` at read time
(`TransactionMapper.ToResponse`) rather than persisting it — it's a pure function of already-stored
data, so persisting it would just be a second place for it to drift out of sync. The review queue
is ordered by that same confidence, ascending, across every one of the user's `PendingReview`
statements — the transactions most likely to be wrong surface first.

### Verification is a human decision, not a computed one

`POST /api/statements/{id}/verify` is the only way a statement reaches `Verified` — nothing marks
a statement verified automatically, no matter how high its classification confidences or how clean
its reconciliation, because "a human looked at this and agreed" is the entire meaning of the state.
It's only valid from `PendingReview` (a statement that's still `Uploaded`/`Processing` hasn't been
classified or reconciled yet, and one already `Verified` doesn't need re-verifying without a new
reprocess putting it back in `PendingReview` first).

## Trigger: synchronous by default, Hangfire job when configured (Phases 11–14)

`POST /api/statements/{id}/reprocess` calls `StatementProcessingService.ProcessAsync` via
`IBackgroundJobScheduler` (Phase 14) — synchronously by default (fast enough, single-digit
milliseconds per statement with the Mock providers and no external calls, that this doesn't
violate the spirit of requirement #11's "don't do *long-running* OCR/AI work in a request"), or
enqueued for a separate `FinancialStatementAI.Worker` process via Hangfire when
`BackgroundJobs:Provider` = `Hangfire`. The endpoint's URL and verb never change between the two;
only the status code does (`200 OK` synchronous, `202 Accepted` when enqueued) — see
`docs/architecture.md`'s Phase 14 section for the full design (including why this lives in
`StatementService.RequestReprocessAsync` rather than on `IStatementProcessingService` itself, to
avoid a circular dependency) and Phase 15 for the per-statement lock that keeps two overlapping
runs of this same method from corrupting each other's writes.
