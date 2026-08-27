"""Thin wrapper around PaddleOCR's PP-StructureV3 pipeline for document layout / table structure
analysis. Same verification caveat as ocr_engine.py: written against the documented 3.x pipeline
API but not runnable in the environment this project was built in — verify the result shape
against your installed `paddleocr` version.
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
    return PPStructureV3()


def _extract_tables(raw_result) -> list[RecognizedTable]:
    data = getattr(raw_result, "json", None) or getattr(raw_result, "res", None) or raw_result
    blocks = data.get("parsing_res_list") or data.get("layout_parsing_result") or [] if isinstance(data, dict) else []

    tables: list[RecognizedTable] = []
    for block in blocks:
        block_type = (block.get("block_label") or block.get("type") or "").lower()
        if block_type != "table":
            continue

        html = block.get("block_content") or block.get("html") or block.get("res", {}).get("html", "")
        if not html:
            continue

        bbox = block.get("block_bbox") or block.get("bbox")
        box = tuple(int(v) for v in bbox) if bbox and len(bbox) == 4 else (0, 0, 0, 0)
        confidence = float(_to_native(block.get("score", block.get("confidence", 0.0))))

        tables.append(RecognizedTable(html=html, confidence=confidence, box=box))

    return tables


def run_structure_analysis(image: Image.Image) -> list[RecognizedTable]:
    import numpy as np

    pipeline = _get_structure_pipeline()
    results = pipeline.predict(np.array(image))

    tables: list[RecognizedTable] = []
    for page_result in results:
        tables.extend(_extract_tables(page_result))
    return tables
