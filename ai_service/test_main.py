"""
Bộ kiểm thử tự động cho NauVi AI Service (FastAPI).
Chạy ở chế độ AI_MODE=mock, KHÔNG gọi Gemini thật.

Lệnh chạy:
    AI_MODE=mock pytest test_main.py -v
"""

import os
import io

# Đảm bảo AI_MODE=mock TRƯỚC khi import main
os.environ["AI_MODE"] = "mock"
os.environ["GEMINI_API_KEY"] = ""

import pytest
from fastapi.testclient import TestClient
from main import app

client = TestClient(app)


# ═══════════════════════════════════════════════════════════════════════════════
# Test 1: Endpoint /health trả về trạng thái healthy ở chế độ mock
# ═══════════════════════════════════════════════════════════════════════════════
class TestHealth:
    def test_health_returns_200(self):
        """GET /health phải trả HTTP 200."""
        res = client.get("/health")
        assert res.status_code == 200

    def test_health_status_is_healthy(self):
        """GET /health phải có status = 'healthy'."""
        data = client.get("/health").json()
        assert data["status"] == "healthy"

    def test_health_shows_mock_mode(self):
        """GET /health phải báo ai_mode = 'mock' khi AI_MODE=mock."""
        data = client.get("/health").json()
        assert data["ai_mode"] == "mock"

    def test_health_has_food_count(self):
        """GET /health phải báo số lượng món ăn đã nạp."""
        data = client.get("/health").json()
        assert "db_foods_loaded" in data
        assert isinstance(data["db_foods_loaded"], int)


# ═══════════════════════════════════════════════════════════════════════════════
# Test 2: Endpoint /predict trả kết quả mock khi gửi ảnh hợp lệ
# ═══════════════════════════════════════════════════════════════════════════════
class TestPredictMock:
    @staticmethod
    def _make_tiny_png() -> bytes:
        """Tạo ảnh PNG 1×1 pixel hợp lệ dùng Pillow."""
        from PIL import Image
        buf = io.BytesIO()
        Image.new("RGB", (1, 1), color="red").save(buf, format="PNG")
        buf.seek(0)
        return buf.read()

    def test_predict_mock_returns_top3(self):
        """POST /predict với ảnh hợp lệ ở chế độ mock phải trả top-3 món."""
        png = self._make_tiny_png()
        res = client.post(
            "/predict",
            files={"file": ("test.png", png, "image/png")}
        )
        assert res.status_code == 200
        data = res.json()
        assert "top3" in data
        assert len(data["top3"]) == 3

    def test_predict_mock_has_confidence(self):
        """Mỗi món trong top-3 mock phải có trường label và confidence."""
        png = self._make_tiny_png()
        data = client.post(
            "/predict",
            files={"file": ("test.png", png, "image/png")}
        ).json()
        for item in data["top3"]:
            assert "label" in item
            assert "confidence" in item
            assert 0 <= item["confidence"] <= 1

    def test_predict_mock_model_version_contains_mock(self):
        """model_version phải chứa chuỗi 'MOCK' để phân biệt rõ ràng."""
        png = self._make_tiny_png()
        data = client.post(
            "/predict",
            files={"file": ("test.png", png, "image/png")}
        ).json()
        assert "MOCK" in data.get("model_version", "").upper()

    def test_predict_rejects_non_image(self):
        """POST /predict với file không phải ảnh phải trả HTTP 400."""
        res = client.post(
            "/predict",
            files={"file": ("readme.txt", b"hello world", "text/plain")}
        )
        assert res.status_code == 400

    def test_predict_rejects_oversized_file(self):
        """POST /predict với file > 5MB phải trả HTTP 400."""
        # Tạo ảnh PNG hợp lệ nhỏ rồi đệm thêm byte cho vượt 5MB
        from PIL import Image
        buf = io.BytesIO()
        Image.new("RGB", (1, 1), color="blue").save(buf, format="PNG")
        buf.seek(0)
        big_data = buf.read() + b"\x00" * (6 * 1024 * 1024)  # Pad to >5MB
        res = client.post(
            "/predict",
            files={"file": ("big.png", big_data, "image/png")}
        )
        assert res.status_code == 400
