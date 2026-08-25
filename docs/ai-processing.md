# AI / Document Processing

> Built up phase by phase. This revision covers **Phase 7 (direct PDF text extraction and the
> OCR-vs-direct decision)** and **Phase 8 (OCR / Document Intelligence abstractions)**.
> Transaction extraction/normalization (Phase 9), classification (Phase 10), and reconciliation
> (Phase 11) will extend this document as they land.

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

## Trigger: synchronous today, background job from Phase 14

`POST /api/statements/{id}/reprocess` runs `StatementProcessingService.ProcessAsync` and blocks
until it's done — practical to build and test the extraction logic against before Hangfire exists,
and fast enough (single-digit milliseconds per statement, no external calls) that "runs
synchronously" doesn't currently violate the spirit of requirement #11 (don't do *long-running*
OCR/AI work in a request). Once Phase 14 wires up Hangfire, the pending `ProcessingJob` row that
upload already creates (Phase 6) is what actually gets consumed, and this endpoint's contract
changes to "enqueue and return 202 Accepted" without changing its URL or request/response shape.
