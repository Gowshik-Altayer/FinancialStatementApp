"""Thin wrapper around PaddleOCR's PP-OCRv6 pipeline.

Verified against paddleocr==3.7.0 / paddlepaddle==3.3.1 on Windows CPU: `.predict()` returns an
`OCRResult` per page whose `.json` is `{"res": {...actual fields...}}` — the fields
(`rec_texts`/`rec_scores`/`rec_polys`) are nested one level under "res", not at the top level.
`_extract_page_result` unwraps that. Two other things needed for a real run to work at all on
this setup, both applied in `_get_ocr_pipeline`:
  - `enable_mkldnn=False` — with oneDNN acceleration on, PP-OCRv6 inference fails outright with
    "(Unimplemented) ConvertPirAttribute2RuntimeAttribute not support [...DoubleAttribute]", a
    PaddlePaddle PIR-executor/oneDNN incompatibility on this CPU build, not anything in this code.
  - `use_textline_orientation` — the 3.x parameter name; the older `use_angle_cls` name is
    silently accepted into **kwargs and does nothing, so passing only the old name looks like it
    worked (no error) while quietly not enabling the orientation classifier at all.
"""

import logging
from dataclasses import dataclass
from functools import lru_cache

import numpy as np
from PIL import Image

logger = logging.getLogger("ocr-service")


@dataclass
class RecognizedTextBlock:
    text: str
    confidence: float
    box: tuple[int, int, int, int]  # x1, y1, x2, y2


@lru_cache(maxsize=1)
def _get_ocr_pipeline():
    # Imported lazily (and cached as a singleton) so the module can be imported — e.g. for
    # /health checks — without paying PaddleOCR's model-loading cost, and so the (large) model
    # download only ever happens once per container lifetime, not per request.
    from paddleocr import PaddleOCR

    logger.info("Loading PP-OCRv6 pipeline (first call only; downloads model weights on first run)...")
    return PaddleOCR(ocr_version="PP-OCRv6", lang="en", use_textline_orientation=True, enable_mkldnn=False)


def _to_native(value):
    """Converts numpy scalars (which PaddleOCR/PaddlePaddle results are full of) to plain
    Python types so FastAPI/Pydantic can JSON-serialize them without a custom encoder."""
    if isinstance(value, (np.floating,)):
        return float(value)
    if isinstance(value, (np.integer,)):
        return int(value)
    return value


def _bounding_box_from_polygon(polygon) -> tuple[int, int, int, int]:
    """PaddleOCR reports each detected region as a 4-point polygon (it detects rotated/skewed
    text, not just axis-aligned boxes) — collapse it to an axis-aligned bounding box, since that's
    a simpler, sufficient shape for this project's needs (highlighting/reviewing a region, not
    reproducing exact rotation)."""
    xs = [float(point[0]) for point in polygon]
    ys = [float(point[1]) for point in polygon]
    return int(min(xs)), int(min(ys)), int(max(xs)), int(max(ys))


def _extract_page_result(raw_result) -> list[RecognizedTextBlock]:
    """raw_result is one page's result from PaddleOCR.predict(). Its exact shape varies by
    version — some expose a dict-like `.json`/`res` payload with `rec_texts`/`rec_scores`/
    `rec_polys` (or `dt_polys`) keys; older releases return a plain list of
    [polygon, (text, score)] tuples via `.ocr()`. Both are handled here."""
    data = getattr(raw_result, "json", None) or getattr(raw_result, "res", None) or raw_result

    # `.json` on paddleocr 3.7.0's OCRResult is {"res": {...actual fields...}} — the fields we
    # want are nested one level deeper, not at the top level.
    if isinstance(data, dict) and isinstance(data.get("res"), dict):
        data = data["res"]

    if isinstance(data, dict):
        texts = data.get("rec_texts") or data.get("texts") or []
        scores = data.get("rec_scores") or data.get("scores") or []
        polys = data.get("rec_polys") or data.get("dt_polys") or data.get("boxes") or []

        blocks = []
        for text, score, polygon in zip(texts, scores, polys):
            blocks.append(RecognizedTextBlock(
                text=str(text),
                confidence=float(_to_native(score)),
                box=_bounding_box_from_polygon(polygon)
            ))
        return blocks

    # Fallback: legacy list-of-[polygon, (text, score)] shape from PaddleOCR's older `.ocr()` API.
    blocks = []
    for line in data or []:
        polygon, (text, score) = line
        blocks.append(RecognizedTextBlock(text=str(text), confidence=float(score), box=_bounding_box_from_polygon(polygon)))
    return blocks


def run_ocr(image: Image.Image) -> list[RecognizedTextBlock]:
    pipeline = _get_ocr_pipeline()
    results = pipeline.predict(np.array(image))

    blocks: list[RecognizedTextBlock] = []
    for page_result in results:
        blocks.extend(_extract_page_result(page_result))
    return blocks
