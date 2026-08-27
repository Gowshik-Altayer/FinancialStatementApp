# Sample Data

Synthetic bank statements for exercising the document-processing pipeline end-to-end, including
the real PaddleOCR integration (`ocr-service/`). None of these are real account data — every
name, account number, and transaction is fabricated for testing.

| File | Format | Exercises |
|---|---|---|
| `digital-bank-statement.pdf` | PDF with an embedded text layer | Phase 7's direct PDF text extraction (PdfPig) — no OCR involved |
| `scanned-bank-statement.png` | PNG image, no text layer | The OCR path directly — images always go straight to `IOcrService` (PP-OCRv6), then `IDocumentIntelligenceService` (PP-StructureV3) for table reconstruction |
| `scanned-bank-statement.pdf` | PDF containing only an embedded JPEG page image, no text layer | The realistic "scanned PDF" case: `StatementProcessingService` tries direct PDF extraction first, finds no usable text, and falls back to OCR — the same rendered statement as the PNG above, wrapped as a PDF |

## How to test against the real OCR service

1. Start `ocr-service/` (see its own `README.md`) — either `docker compose up ocr-service` from
   the repo root, or run it standalone with `uvicorn app.main:app --reload` from `ocr-service/`.
2. Upload `scanned-bank-statement.png` or `scanned-bank-statement.pdf` via
   `POST /api/statements/upload`, then `POST /api/statements/{id}/reprocess`.
3. Check the response's `extractionMethod` (should be `"Ocr"`) and, once available, the
   `StatementExtraction.ConfidenceScore`/`OcrTextBlocks`/`OcrTableRegions` rows in the database —
   see [docs/database.md](../docs/database.md) and [docs/ai-processing.md](../docs/ai-processing.md).
4. `digital-bank-statement.pdf` is a good control case: it should reach `PendingReview` via
   `extractionMethod: "DirectPdfText"` without ever calling the OCR service at all.

Regenerate or extend these files with the scripts described below — there's no need to hand-craft
a PDF or image; both are built programmatically so the "ground truth" transaction data is exact
and can be diffed against what the pipeline extracts.
