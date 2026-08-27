# OCR Service (PaddleOCR)

A small FastAPI microservice wrapping PaddleOCR's **PP-OCRv6** (text detection/recognition) and
**PP-StructureV3** (document layout / table structure) pipelines. The .NET application
(`FinancialStatementAI.Infrastructure`'s `PaddleOcrService`/`PaddleDocumentStructureService`)
calls this over HTTP — see `docs/ai-processing.md` for why OCR has to live in a separate service
rather than in-process (PaddleOCR is Python/PaddlePaddle-only; there's no viable native .NET port).

## Endpoints

- `GET /health` — liveness check.
- `POST /ocr` (multipart `file` field) — runs PP-OCRv6 on a PDF or image. Returns extracted text
  per page, per-line bounding boxes, confidence scores, and an overall confidence average.
- `POST /structure` (multipart `file` field) — runs PP-StructureV3 layout analysis and returns any
  detected tables as HTML, with bounding boxes and confidence.

PDFs are rasterized to one image per page (`app/pdf_utils.py`, via `pypdfium2`) before either
pipeline runs, since both operate on images.

## Running locally

```bash
python -m venv .venv
source .venv/bin/activate  # or .venv\Scripts\activate on Windows
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

First request to `/ocr` or `/structure` downloads the PP-OCRv6 / PP-StructureV3 model weights
(a few hundred MB each) and caches them — subsequent requests reuse the cached models. In Docker,
mount a volume over PaddleOCR's model cache directory (see `docker-compose.yml` at the repo root)
so this download only happens once, not on every container start.

## Running via Docker

```bash
docker build -t financialstatementai-ocr-service .
docker run -p 8000:8000 financialstatementai-ocr-service
```

Or via the repo root's `docker-compose.yml`, which wires this up alongside the rest of the stack.

## Verified against a real run (paddleocr==3.7.0, paddlepaddle==3.3.1, Windows CPU)

This service was originally written without any Python environment available to test it in. It
has since been run for real against a sample scanned statement, which surfaced three real bugs —
all fixed, all reflected in the code below:

1. **`enable_mkldnn=False` is required.** With oneDNN acceleration on (the default), inference
   fails outright with `(Unimplemented) ConvertPirAttribute2RuntimeAttribute not support
   [...DoubleAttribute]` — a PaddlePaddle PIR-executor/oneDNN incompatibility on this build, not a
   bug in this code. Both `_get_ocr_pipeline` and `_get_structure_pipeline` now pass it explicitly.
2. **`.json`'s payload is nested one level deeper than assumed.** `PaddleOCR.predict()`/
   `PPStructureV3.predict()` results expose `.json` as `{"res": {...actual fields...}}`, not the
   fields directly — `_extract_page_result`/`_extract_tables` now unwrap `"res"` before reading
   `rec_texts`/`rec_scores`/`rec_polys`/`parsing_res_list`.
3. **`PP-StructureV3` needs `paddlex[ocr]`, not just `paddleocr`.** Without it, constructing the
   pipeline raises `DependencyError: PP-StructureV3 requires additional dependencies` — now in
   `requirements.txt`.

With those fixed, a real request against a synthetic scanned bank statement
(`sample-data/scanned-bank-statement.png` at the repo root) correctly returned all 61 detected
text regions and reconstructed the transaction table into accurate HTML (confidence ~0.99),
end-to-end through the .NET pipeline (`transactionCount` on the resulting statement matched the
source exactly).

### ⚠️ PP-StructureV3 is memory-hungry — expect occasional crashes on modest hardware

`PPStructureV3()` loads roughly a dozen separate models at once (layout detection, block layout,
text-line/doc orientation, doc unwarping, two table classifiers, two table-structure recognizers,
two table-cell detectors, and a formula-recognition network). On a CPU-only machine without much
free RAM, this pipeline's first call after a (re)start silently kills the whole process with **no
Python traceback at all** — consistent with either an OS-level OOM kill or a native-level crash in
PaddlePaddle's C++ backend, neither of which Python can catch. Observed repeatedly in testing,
always at the same point (loading `PP-FormulaNet_plus-L`), non-deterministically depending on what
else was running on the machine at the time.

This is a genuine resource-intensity characteristic of PP-StructureV3 on CPU, not a defect in this
code — and the .NET side already treats it as such: `StatementProcessingService.
TryDocumentStructureAnalysisAsync` catches any failure (including the connection reset a crashed
service produces) and simply proceeds without table data, never failing the reprocess. PP-OCRv6
alone (the `/ocr` endpoint, `Ocr:Provider`) was never observed to crash in the same testing and is
far lighter — if PP-StructureV3 proves unreliable on your machine, set `DocumentIntelligence:
Provider` to `Mock` (see `appsettings.json`) to keep OCR working without it; `docker-compose.yml`'s
containers, or a machine with more free RAM / a GPU build of PaddlePaddle, are more realistic ways
to run it reliably than a memory-constrained CPU-only dev laptop.
