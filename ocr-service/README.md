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

## ⚠️ Environment note: unverified in the sandbox this was built in

This service was written against PaddleOCR's documented 3.x `.predict()` pipeline API, but the
development environment used to build it had **no Python installed at all** — it could not be
installed, run, or tested here. `ocr_engine.py` and `structure_engine.py` defensively check
several known result-key names (PaddleOCR's exact result object shape has shifted across
releases), but you should:

1. Install the requirements and run a real request against `/ocr` and `/structure` with a sample
   statement before relying on this in any real workflow.
2. If the response comes back with no text blocks / no tables despite a document that clearly has
   both, add a `print(raw_result)` / log statement in `_extract_page_result` /
   `_extract_tables` to see the actual result shape your installed `paddleocr` version returns,
   and adjust the key names there to match.

This is the same class of limitation as this repository's Docker/Redis setup — written carefully,
but needs a first real run in an environment where the tooling actually exists before being
trusted.
