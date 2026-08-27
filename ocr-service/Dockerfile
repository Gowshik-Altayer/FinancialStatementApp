# CPU-only image — see requirements.txt for the GPU swap. Build context is this directory:
#   docker build -t financialstatementai-ocr-service .
FROM python:3.11-slim

WORKDIR /app

# libgl1/libglib2.0-0 are OpenCV's (a PaddleOCR dependency) runtime requirements, not present in
# the slim base image.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 \
    libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app/ app/

EXPOSE 8000
ENTRYPOINT ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
