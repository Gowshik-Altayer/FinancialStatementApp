"""Thin wrapper around PaddleOCR's PP-StructureV3 pipeline for document layout / table structure
analysis.

Verified against paddleocr==3.7.0 / paddlepaddle==3.3.1 on Windows CPU (same `enable_mkldnn=False`
requirement as ocr_engine.py — see its module docstring). `.predict()`'s `.json` is likewise
`{"res": {...}}`; within "res", the field that actually matters here is `parsing_res_list` — a
flat list of layout blocks (paragraph, title, table, ...) in document order, each with
`block_label`/`block_content`/`block_bbox`. A `block_label == "table"`'s `block_content` is
already the table's HTML and `block_bbox` its box — no separate lookup needed for those. The one
thing NOT in that block is a confidence score, so this pairs each table block (by document order)
with the same-index entry in the separate `table_res_list`, which has a `table_ocr_pred.rec_scores`
list (per detected cell) that gets averaged into one table-level confidence — `parsing_res_list`
and `table_res_list` reliably run in the same table order in the shape actually observed, though
that's an inferred pairing rather than an explicit index the API guarantees.
"""

import logging
from dataclasses import dataclass
from functools import lru_cache

from PIL import Image

from app.ocr_engine import _to_native

logger = logging.getLogger("ocr-service")


@dataclass
class RecognizedTable:
    html: str
    confidence: float
    box: tuple[int, int, int, int]


@lru_cache(maxsize=1)
def _get_structure_pipeline():
    from paddleocr import PPStructureV3

    logger.info("Loading PP-StructureV3 pipeline (first call only; downloads model weights on first run)...")
    return PPStructureV3(enable_mkldnn=False)


def _table_confidence(table_res_list: list, table_index: int) -> float:
    """Average of the per-cell OCR confidences for the table at this index, or 0.0 if that data
    isn't available — see module docstring for why this needs a separate lookup at all."""
    if table_index >= len(table_res_list):
        return 0.0

    scores = table_res_list[table_index].get("table_ocr_pred", {}).get("rec_scores") or []
    return float(sum(_to_native(s) for s in scores) / len(scores)) if scores else 0.0


def _extract_tables(raw_result) -> list[RecognizedTable]:
    data = getattr(raw_result, "json", None) or getattr(raw_result, "res", None) or raw_result

    # `.json` on paddleocr 3.7.0's LayoutParsingResultV2 is {"res": {...actual fields...}} — see
    # module docstring.
    if isinstance(data, dict) and isinstance(data.get("res"), dict):
        data = data["res"]

    blocks = data.get("parsing_res_list") or data.get("layout_parsing_result") or [] if isinstance(data, dict) else []
    table_res_list = data.get("table_res_list") or [] if isinstance(data, dict) else []

    tables: list[RecognizedTable] = []
    table_index = 0
    for block in blocks:
        block_type = (block.get("block_label") or block.get("type") or "").lower()
        if block_type != "table":
            continue

        html = block.get("block_content") or block.get("html") or block.get("res", {}).get("html", "")
        if not html:
            table_index += 1
            continue

        bbox = block.get("block_bbox") or block.get("bbox")
        box = tuple(int(v) for v in bbox) if bbox and len(bbox) == 4 else (0, 0, 0, 0)
        confidence = _table_confidence(table_res_list, table_index)

        tables.append(RecognizedTable(html=html, confidence=confidence, box=box))
        table_index += 1

    return tables


def run_structure_analysis(image: Image.Image) -> list[RecognizedTable]:
    import numpy as np

    pipeline = _get_structure_pipeline()
    results = pipeline.predict(np.array(image))

    tables: list[RecognizedTable] = []
    for page_result in results:
        tables.extend(_extract_tables(page_result))
    return tables
