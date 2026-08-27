"""Renders each page of a PDF to a PIL image so PaddleOCR's pipelines (which operate on images,
not PDF bytes) can process it — one image per page, at a high enough DPI to keep small statement
text legible to the recognition model.
"""

import io

import pypdfium2 as pdfium
from PIL import Image

RENDER_DPI = 200
_PDF_MAGIC = b"%PDF-"


def is_pdf(content: bytes) -> bool:
    return content[:5] == _PDF_MAGIC


def render_pdf_pages(content: bytes) -> list[Image.Image]:
    """Returns one RGB PIL image per page, in page order."""
    pdf = pdfium.PdfDocument(io.BytesIO(content))
    scale = RENDER_DPI / 72  # PDF user space is 72 DPI by definition

    pages: list[Image.Image] = []
    try:
        for page in pdf:
            bitmap = page.render(scale=scale)
            pages.append(bitmap.to_pil().convert("RGB"))
    finally:
        pdf.close()

    return pages


def load_image(content: bytes) -> Image.Image:
    return Image.open(io.BytesIO(content)).convert("RGB")


def load_pages(content: bytes) -> list[Image.Image]:
    """Single entry point for both endpoints: PDFs become one image per page, a plain image
    becomes a single-page list — callers never need to branch on content type themselves."""
    return render_pdf_pages(content) if is_pdf(content) else [load_image(content)]
