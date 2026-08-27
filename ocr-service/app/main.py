"""OCR microservice: wraps PaddleOCR's PP-OCRv6 (text recognition) and PP-StructureV3 (document
layout / table structure) pipelines behind a small HTTP API the .NET application calls, kept
deliberately separate from the .NET business logic (see docs/ai-processing.md's OCR section for
why — this is a Python-only ML stack with no viable native .NET port).
"""

import logging

from fastapi import FastAPI, File, HTTPException, UploadFile

from app.models import OcrPage, OcrResponse, StructureResponse, TableResult, TextBlock
from app.ocr_engine import run_ocr
from app.pdf_utils import load_pages
from app.structure_engine import run_structure_analysis

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("ocr-service")

app = FastAPI(title="FinancialStatementAI OCR Service", version="1.0.0")

MAX_UPLOAD_BYTES = 25 * 1024 * 1024  # matches the .NET Api's own upload limit


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/ocr", response_model=OcrResponse)
async def extract_text(file: UploadFile = File(...)):
    content = await file.read()
    if len(content) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail="File exceeds the 25 MB limit.")

    try:
        images = load_pages(content)
    except Exception as ex:  # noqa: BLE001 — any failure here is a legitimate "OCR failed" outcome
        logger.exception("Failed to load pages from uploaded content")
        return OcrResponse(success=False, errorMessage=f"Could not read the document: {ex}")

    pages: list[OcrPage] = []
    all_text_parts: list[str] = []
    all_confidences: list[float] = []

    for page_number, image in enumerate(images, start=1):
        try:
            blocks = run_ocr(image)
        except Exception as ex:  # noqa: BLE001
            logger.exception("OCR failed on page %s", page_number)
            return OcrResponse(success=False, errorMessage=f"OCR failed on page {page_number}: {ex}")

        text_blocks = [
            TextBlock(text=b.text, confidence=b.confidence, x1=b.box[0], y1=b.box[1], x2=b.box[2], y2=b.box[3])
            for b in blocks
        ]
        pages.append(OcrPage(pageNumber=page_number, textBlocks=text_blocks))
        all_text_parts.append("\n".join(b.text for b in blocks))
        all_confidences.extend(b.confidence for b in blocks)

    overall_confidence = sum(all_confidences) / len(all_confidences) if all_confidences else None

    return OcrResponse(
        success=True,
        rawText="\n".join(all_text_parts),
        confidence=overall_confidence,
        pages=pages
    )


@app.post("/structure", response_model=StructureResponse)
async def analyze_structure(file: UploadFile = File(...)):
    content = await file.read()
    if len(content) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail="File exceeds the 25 MB limit.")

    try:
        images = load_pages(content)
    except Exception as ex:  # noqa: BLE001
        logger.exception("Failed to load pages from uploaded content")
        return StructureResponse(success=False, errorMessage=f"Could not read the document: {ex}")

    tables: list[TableResult] = []
    for page_number, image in enumerate(images, start=1):
        try:
            page_tables = run_structure_analysis(image)
        except Exception as ex:  # noqa: BLE001
            logger.exception("Structure analysis failed on page %s", page_number)
            return StructureResponse(success=False, errorMessage=f"Structure analysis failed on page {page_number}: {ex}")

        tables.extend(
            TableResult(pageNumber=page_number, html=t.html, confidence=t.confidence, x1=t.box[0], y1=t.box[1], x2=t.box[2], y2=t.box[3])
            for t in page_tables
        )

    return StructureResponse(success=True, tables=tables)
