"""
NauVi AI Service v7.0 – PHIÊN BẢN CUỐI CÙNG, SỬA DỨT ĐIỂM
=============================================================
- Engine: Gemini Vision API (thử nhiều model, ưu tiên model ổn định nhất)
- KHÔNG CÓ FALLBACK ĐÁT MÒ — nếu AI thất bại thì trả 503 rõ ràng
- Tự động ánh xạ tên món AI trả về → tên chính xác trong CSDL NauVi
- Retry tối đa 2 lần cho lỗi 503 tạm thời
"""

import os
import io
import re
import json
import base64
import asyncio
import unicodedata
from pathlib import Path

from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse
from PIL import Image
import httpx

app = FastAPI(title="NauVi AI Food Recognition", version="7.0.0")


# ═══════════════════════════════════════════════════════════════════════════════
# 1. NẠP CẤU HÌNH
# ═══════════════════════════════════════════════════════════════════════════════

def _load_env():
    """Đọc biến môi trường từ file .env (thư mục ai_service hoặc project root)."""
    for env_path in [
        Path(__file__).parent / ".env",
        Path(__file__).parent.parent / ".env",
    ]:
        if env_path.exists():
            for line in env_path.read_text(encoding="utf-8").splitlines():
                line = line.strip()
                if not line or line.startswith("#") or "=" not in line:
                    continue
                k, v = line.split("=", 1)
                k, v = k.strip(), v.strip()
                if k and not os.environ.get(k):
                    os.environ[k] = v

_load_env()


def _load_foods() -> list:
    """Nạp danh sách món ăn từ foods.json (SeedData)."""
    for p in [
        Path(__file__).parent.parent / "SeedData" / "foods.json",
        Path(__file__).parent / "foods.json",
    ]:
        if p.exists():
            try:
                return json.loads(p.read_text(encoding="utf-8"))
            except Exception:
                pass
    return []


ALL_FOODS = _load_foods()
ALL_FOOD_NAMES = [f["name"] for f in ALL_FOODS if f.get("name")]
MAX_FILE_SIZE = 5 * 1024 * 1024  # 5 MB


# ═══════════════════════════════════════════════════════════════════════════════
# 2. ÁNH XẠ TÊN MÓN (FUZZY MATCH)
# ═══════════════════════════════════════════════════════════════════════════════

def _normalize(text: str) -> str:
    """Chuẩn hóa Unicode, bỏ dấu, lowercase để so sánh fuzzy."""
    text = text.lower().strip()
    # Bỏ dấu tiếng Việt
    nfkd = unicodedata.normalize("NFKD", text)
    no_diacritics = "".join(c for c in nfkd if not unicodedata.combining(c))
    # Bỏ ký tự đặc biệt, chỉ giữ chữ và số
    return re.sub(r"[^a-z0-9 ]", "", no_diacritics).strip()


# Xây bảng tra cứu tên chuẩn hóa → tên gốc trong DB
_NORMALIZED_FOOD_MAP: dict[str, str] = {}
for _name in ALL_FOOD_NAMES:
    _NORMALIZED_FOOD_MAP[_normalize(_name)] = _name


def _match_food_name(ai_label: str) -> str:
    """
    Ánh xạ tên món do AI trả về (có thể viết tắt, thiếu dấu, khác tên)
    sang tên chính xác trong CSDL NauVi.
    Ví dụ: "phở bò" → "Phở Bò Truyền Thống", "com suon" → "Cơm Sườn Chả"
    """
    norm_label = _normalize(ai_label)

    # 1. Khớp chính xác sau chuẩn hóa
    if norm_label in _NORMALIZED_FOOD_MAP:
        return _NORMALIZED_FOOD_MAP[norm_label]

    # 2. Tìm tên DB nào chứa tên AI (substring match)
    best_match = None
    best_score = 0
    for norm_db, original_name in _NORMALIZED_FOOD_MAP.items():
        # Kiểm tra AI label có nằm trong tên DB không (hoặc ngược lại)
        if norm_label in norm_db or norm_db in norm_label:
            # Ưu tiên match dài hơn (cụ thể hơn)
            score = len(set(norm_label.split()) & set(norm_db.split()))
            if score > best_score:
                best_score = score
                best_match = original_name

    if best_match:
        return best_match

    # 3. Tìm theo từ khóa chính (từ đầu tiên có nghĩa)
    label_words = set(norm_label.split())
    best_match = None
    best_score = 0
    for norm_db, original_name in _NORMALIZED_FOOD_MAP.items():
        db_words = set(norm_db.split())
        common = label_words & db_words
        if len(common) > best_score:
            best_score = len(common)
            best_match = original_name

    if best_match and best_score >= 1:
        return best_match

    # 4. Không tìm thấy → giữ nguyên tên AI trả về
    return ai_label


# ═══════════════════════════════════════════════════════════════════════════════
# 3. GỌI GEMINI VISION API
# ═══════════════════════════════════════════════════════════════════════════════

def _extract_json_top3(text: str) -> list | None:
    """Trích xuất danh sách top3 từ chuỗi JSON (có thể lẫn markdown)."""
    if not text:
        return None
    try:
        # Tìm khối JSON ngoài cùng
        match = re.search(r'\{[\s\S]*\}', text)
        if match:
            parsed = json.loads(match.group(0))
            top3 = parsed.get("top3", [])
            if isinstance(top3, list) and len(top3) > 0:
                result = []
                for item in top3[:3]:
                    if isinstance(item, dict) and "label" in item:
                        label = str(item["label"]).strip()
                        conf = float(item.get("confidence", 0.5))
                        # Ánh xạ tên AI → tên chính xác trong CSDL
                        matched_name = _match_food_name(label)
                        result.append({
                            "label": matched_name,
                            "confidence": round(conf, 4)
                        })
                if result:
                    return result
    except Exception as e:
        print(f"[JSON Parse] {e}")
    return None


async def _call_gemini_with_retry(
    image_bytes: bytes,
    mime_type: str,
    api_key: str,
    max_retries: int = 2
) -> tuple[list | None, str]:
    """
    Gọi Gemini Vision API với retry logic.
    Trả về (top3_list, model_name) hoặc (None, error_msg).
    """
    menu_list = ", ".join(ALL_FOOD_NAMES) if ALL_FOOD_NAMES else \
        "Phở Bò Truyền Thống, Bánh Mì Kẹp Thịt, Cơm Sườn Chả"

    prompt = f"""Bạn là chuyên gia nhận diện món ăn cho ứng dụng NauVi.

DANH SÁCH MÓN ĂN TRONG CƠ SỞ DỮ LIỆU:
[{menu_list}]

NHIỆM VỤ:
1. Quan sát kỹ bức ảnh món ăn.
2. Xác định đây là món gì (ẩm thực Việt Nam hoặc quốc tế).
3. Chọn TOP 3 món gần đúng nhất từ DANH SÁCH trên. BẮT BUỘC dùng đúng tên món có trong danh sách.
4. Nếu món trong ảnh không khớp 100% với danh sách, chọn món gần giống nhất.

TRẢ VỀ JSON (không giải thích thêm):
{{
  "top3": [
    {{"label": "Tên chính xác từ danh sách", "confidence": 0.92}},
    {{"label": "Tên chính xác từ danh sách", "confidence": 0.05}},
    {{"label": "Tên chính xác từ danh sách", "confidence": 0.03}}
  ]
}}"""

    b64 = base64.b64encode(image_bytes).decode("utf-8")

    # Danh sách model theo thứ tự ưu tiên: ổn định nhất trước
    models = [
        "gemini-flash-lite-latest",   # Nhanh ~3s, ổn định nhất hiện tại
        "gemini-3.5-flash-lite",      # Nhanh, có thể 503 khi cao tải
        "gemini-2.5-flash",           # Tốt nhưng hay hết quota free tier
        "gemini-3.5-flash",           # Mạnh hơn, có thể 503
        "gemini-3.8-flash",           # Mới nhất
    ]

    last_error = "Không có model nào phản hồi"

    for model in models:
        url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"
        payload = {
            "contents": [{
                "parts": [
                    {"text": prompt},
                    {"inlineData": {"mimeType": mime_type, "data": b64}}
                ]
            }],
            "generationConfig": {
                "responseMimeType": "application/json",
                "temperature": 0.1,
                "maxOutputTokens": 2048
            }
        }

        # Retry cho lỗi 503 tạm thời
        for attempt in range(max_retries):
            try:
                async with httpx.AsyncClient(timeout=25.0) as client:
                    res = await client.post(url, json=payload)

                if res.status_code == 200:
                    data = res.json()
                    candidates = data.get("candidates", [])
                    if candidates:
                        text = candidates[0].get("content", {}) \
                            .get("parts", [{}])[0].get("text", "")
                        top3 = _extract_json_top3(text)
                        if top3:
                            print(f"[OK] {model} trả kết quả thành công (attempt {attempt+1})")
                            return top3, model
                        else:
                            last_error = f"{model}: JSON không hợp lệ"
                            print(f"[WARN] {model}: JSON parse failed: {text[:150]}")
                    else:
                        last_error = f"{model}: Không có candidates"

                elif res.status_code == 503:
                    # Model quá tải → retry sau 1.5 giây
                    last_error = f"{model}: Quá tải (503)"
                    print(f"[RETRY] {model} 503, attempt {attempt+1}/{max_retries}")
                    if attempt < max_retries - 1:
                        await asyncio.sleep(1.5)
                    continue

                elif res.status_code == 429:
                    # Hết quota → chuyển model khác, không retry
                    last_error = f"{model}: Hết quota (429)"
                    print(f"[SKIP] {model} hết quota 429")
                    break

                else:
                    err_msg = res.json().get("error", {}).get("message", "")[:100]
                    last_error = f"{model}: HTTP {res.status_code} - {err_msg}"
                    print(f"[ERR] {model}: HTTP {res.status_code}: {err_msg}")
                    break  # Lỗi khác → chuyển model

            except httpx.TimeoutException:
                last_error = f"{model}: Timeout"
                print(f"[TIMEOUT] {model}, attempt {attempt+1}")
                if attempt < max_retries - 1:
                    await asyncio.sleep(1.0)
                continue

            except Exception as e:
                last_error = f"{model}: {str(e)[:80]}"
                print(f"[EXC] {model}: {e}")
                break

    return None, last_error


# ═══════════════════════════════════════════════════════════════════════════════
# 4. ENDPOINTS
# ═══════════════════════════════════════════════════════════════════════════════

@app.get("/health")
def health():
    key = os.getenv("GEMINI_API_KEY", "")
    return {
        "status": "ok",
        "version": "7.0.0",
        "engine": "Gemini Vision API (multi-model failover)",
        "has_api_key": bool(key),
        "db_foods_loaded": len(ALL_FOOD_NAMES),
        "food_name_index": len(_NORMALIZED_FOOD_MAP),
    }


@app.post("/predict")
async def predict(file: UploadFile = File(...)):
    """
    Nhận ảnh món ăn → gọi Gemini Vision AI → trả top-3 món từ CSDL NauVi.
    KHÔNG CÓ FALLBACK ĐÁT MÒ. Nếu AI không phản hồi → trả 503 rõ ràng.
    """
    # ── Validate đầu vào ──
    if not file.content_type or not file.content_type.startswith("image/"):
        raise HTTPException(400, detail="Tập tin tải lên phải là hình ảnh (image/*).")

    image_bytes = await file.read()
    if len(image_bytes) > MAX_FILE_SIZE:
        raise HTTPException(400, detail="Kích thước ảnh vượt quá 5MB.")

    try:
        Image.open(io.BytesIO(image_bytes)).verify()
    except Exception:
        raise HTTPException(400, detail="Tập tin ảnh bị lỗi hoặc không thể đọc.")

    # ── Kiểm tra API key ──
    api_key = os.getenv("GEMINI_API_KEY", "")
    if not api_key:
        raise HTTPException(503, detail="Chưa cấu hình GEMINI_API_KEY. Vui lòng thêm key vào file .env.")

    # ── Gọi Gemini Vision AI ──
    mime_type = file.content_type or "image/jpeg"
    top3, model_or_error = await _call_gemini_with_retry(image_bytes, mime_type, api_key)

    if top3:
        return JSONResponse(content={
            "top3": top3,
            "model_version": model_or_error
        })

    # ── AI thất bại → trả 503 rõ ràng, KHÔNG đoán mò ──
    raise HTTPException(
        503,
        detail=f"Dịch vụ AI không thể phân tích ảnh lúc này. Chi tiết: {model_or_error}"
    )
