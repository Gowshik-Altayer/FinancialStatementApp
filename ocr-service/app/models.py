"""Wire-format response models for the OCR microservice. Field names are camelCase to match the
.NET side's JSON deserialization (PaddleOcrService.cs / PaddleDocumentStructureService.cs) without
needing a naming-policy translation layer on either side.
"""

from pydantic import BaseModel


class TextBlock(BaseModel):
    text: str
    confidence: float
    x1: int
    y1: int
    x2: int
    y2: int


class OcrPage(BaseModel):
    pageNumber: int
    textBlocks: list[TextBlock]


class OcrResponse(BaseModel):
    success: bool
    rawText: str = ""
    confidence: float | None = None
    pages: list[OcrPage] = []
    errorMessage: str | None = None


class TableResult(BaseModel):
    pageNumber: int
    html: str
    confidence: float
    x1: int
    y1: int
    x2: int
    y2: int


class StructureResponse(BaseModel):
    success: bool
    tables: list[TableResult] = []
    errorMessage: str | None = None
