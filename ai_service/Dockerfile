# ==========================================
# Dockerfile cho AI Service (FastAPI)
# Base image: python:3.11-slim-bookworm (phiên bản ổn định, tối ưu kích thước)
# ==========================================
FROM python:3.11-slim-bookworm

# Đặt biến môi trường Python tối ưu container
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PORT=8001

WORKDIR /app

# Cài đặt dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Tạo người dùng không phải root (appuser)
RUN groupadd -r appuser && useradd -r -g appuser -d /app -s /sbin/nologin appuser

# Copy mã nguồn service và file dữ liệu món ăn
COPY main.py .
COPY foods.json ./

# Phân quyền thư mục làm việc cho appuser
RUN chown -R appuser:appuser /app

# Đổi sang người dùng appuser
USER appuser

EXPOSE 8001

# HEALTHCHECK gọi endpoint /health bằng Python có sẵn trong ảnh
HEALTHCHECK --interval=20s --timeout=5s --start-period=5s --retries=3 \
    CMD python -c "import urllib.request, sys; sys.exit(0 if urllib.request.urlopen('http://localhost:8001/health').getcode() == 200 else 1)"

# Khởi chạy FastAPI bằng uvicorn
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8001"]
