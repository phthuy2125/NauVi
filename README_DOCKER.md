# Hướng dẫn đóng gói và triển khai NauVi bằng Docker

Tài liệu này hướng dẫn người khác (Giảng viên hướng dẫn, thành viên hội đồng hoặc bạn cùng nhóm) triển khai và chạy thử toàn bộ hệ sinh thái **NauVi** trên bất kỳ máy tính nào chỉ với một câu lệnh Docker Compose.

---

## 1. Yêu cầu hệ thống

Trước khi bắt đầu, hãy đảm bảo máy tính đã cài đặt:
- **Docker** (phiên bản 24.0+ trở lên)
- **Docker Compose** v2 (đi kèm sẵn trong Docker Desktop trên Windows/macOS hoặc cài qua `docker-compose-plugin` trên Linux)

---

## 2. Các bước triển khai

### Bước 1: Tải mã nguồn (Clone Repository)
```bash
git clone https://github.com/phthuy2125/NauVi.git
cd NauVi
```

### Bước 2: Tạo tệp cấu hình môi trường (.env)
Sao chép tệp mẫu `.env.example` sang `.env`:
```bash
# Trên Linux / macOS / Git Bash:
cp .env.example .env

# Hoặc trên Windows PowerShell:
Copy-Item .env.example .env
```

Mở tệp `.env` vừa tạo để cấu hình chế độ chạy AI:
```ini
# Cổng truy cập ứng dụng (mặc định: 7200)
PORT=7200

# Chế độ AI Service:
# - AI_MODE=mock: Dành cho chạy thử nghiệm không cần API Key, hệ thống trả về kết quả giả lập mẫu.
# - AI_MODE=real: Nhận diện ảnh thật bằng Google Gemini Vision (cần điền GEMINI_API_KEY).
AI_MODE=mock
GEMINI_API_KEY=YOUR_GEMINI_API_KEY_HERE

# Tự động nạp dữ liệu món ăn mẫu chuẩn vào MongoDB khi khởi động
SEED_ON_STARTUP=true
```

> **Lưu ý quan trọng về `AI_MODE`**:
> - Khi chấm bài hoặc chạy thử trên máy người khác không có khóa API: Hãy để `AI_MODE=mock`. Ứng dụng sẽ nhận diện thành công các món ăn và gắn nhãn rõ ràng `isMock: true` để minh bạch.
> - Khi muốn nhận diện bằng trí tuệ nhân tạo thực: Đổi sang `AI_MODE=real` và dán khóa Google Gemini API hợp lệ.

### Bước 3: Khởi chạy toàn bộ hệ thống
Chạy lệnh sau tại thư mục gốc của dự án:
```bash
docker compose up --build
```
*(Nếu muốn chạy ngầm dưới nền, thêm cờ `-d`: `docker compose up --build -d`)*

Docker Compose sẽ tự động xây dựng và khởi động theo đúng thứ tự phụ thuộc (`service_healthy`):
1. **`nauvi_mongo`** (MongoDB 7.0): Khởi chạy và kiểm tra ping thành công.
2. **`nauvi_ai_service`** (Python 3.11 / FastAPI): Khởi chạy và vượt qua `/health`.
3. **`nauvi_web`** (ASP.NET Core 8): Kết nối vào MongoDB, nạp sẵn 30 món ăn mẫu chuẩn và lắng nghe tại cổng `7200`.

---

## 3. Địa chỉ truy cập và kiểm tra

| Thành phần | Địa chỉ / URL | Mô tả |
| :--- | :--- | :--- |
| **Giao diện NauVi** | [http://localhost:7200](http://localhost:7200) | Trang chủ người dùng và các tính năng |
| **Nhận diện món ăn AI** | [http://localhost:7200/Home/FoodRecognition](http://localhost:7200/Home/FoodRecognition) | Tải ảnh chụp món ăn để nhận diện |
| **Cộng đồng nấu ăn** | [http://localhost:7200/Home/Community](http://localhost:7200/Home/Community) | Xem và chia sẻ bài đăng món ăn |
| **Trang duyệt bài Admin** | [http://localhost:7200/Admin/ManagePosts](http://localhost:7200/Admin/ManagePosts) | Dành cho quản trị viên kiểm duyệt bài |
| **Kiểm tra Health (Web)** | [http://localhost:7200/health](http://localhost:7200/health) | Trả về trạng thái hoạt động của ASP.NET Core |
| **Kiểm tra Health (AI Service)**| [http://localhost:8001/health](http://localhost:8001/health) | Trả về trạng thái hoạt động và `mode` của AI Service |

---

## 4. Thử nghiệm nhận diện ảnh qua API (cURL)

Bạn có thể gửi một tệp ảnh bất kỳ để kiểm tra dịch vụ nhận diện:

```bash
curl -X POST http://localhost:7200/api/foods/predict-image \
  -F "image=@duong/dan/toi/anh_mon_an.jpg"
```

Phản hồi mẫu ở chế độ `AI_MODE=mock`:
```json
{
  "predictions": [
    {
      "name": "Phở Bò",
      "confidence": 0.95,
      "foodId": "6644f123456789abcdef0001",
      "isMock": true
    },
    {
      "name": "Bún Bò Huế",
      "confidence": 0.85,
      "foodId": "6644f123456789abcdef0002",
      "isMock": true
    },
    {
      "name": "Bánh Mì Thịt",
      "confidence": 0.75,
      "foodId": "6644f123456789abcdef0003",
      "isMock": true
    }
  ],
  "uncertain": false,
  "top1Confidence": 0.95,
  "threshold": 0.5,
  "model": "mock-evaluator-v1"
}
```

---

## 5. Dừng và dọn dẹp hệ thống

Để dừng tất cả các dịch vụ:
```bash
docker compose down
```

Nếu muốn dừng và **xoá sạch toàn bộ dữ liệu** (reset database và volume ảnh):
```bash
docker compose down -v
```

---

## 6. Xử lý các lỗi thường gặp

### Lỗi 1: Cổng bị chiếm dụng (Port Conflict)
- **Triệu chứng**: Thông báo `Bind for 0.0.0.0:7200 failed: port is already allocated` hoặc `port 8001 is already allocated`.
- **Cách xử lý**:
  - Mở tệp `.env`, đổi cổng `PORT=7200` sang cổng khác (ví dụ: `PORT=7201` hoặc `PORT=8088`).
  - Hoặc tắt tiến trình đang chiếm cổng trên máy host:
    - *Windows (PowerShell)*: `Stop-Process -Name NauVi -Force`
    - *Linux / macOS*: `sudo lsof -i :7200` rồi `kill -9 <PID>`

### Lỗi 2: File is locked by 'NauVi' khi chạy Visual Studio
- **Triệu chứng**: Khi build trên Visual Studio gặp lỗi `MSB3026/MSB3027: The process cannot access the file ... because it is being used by another process`.
- **Cách xử lý**: Nhấn **Shift + F5** trong Visual Studio để tắt chế độ Debug/Run trước khi bấm Rebuild, hoặc mở PowerShell gõ:
  ```powershell
  Stop-Process -Name NauVi -Force
  ```

### Lỗi 3: Đăng nhập Google báo `JWT contains untrusted 'aud' claim`
- **Triệu chứng**: Màn hình hiện thông báo lỗi token Google từ server.
- **Nguyên nhân & Đã xử lý**: Hệ thống đã bổ sung cơ chế fallback tự động trong `GoogleAuthService.cs`. Token chính chủ do Google phát hành sẽ được xác thực qua chứng chỉ mật mã công khai của Google ngay cả khi Client ID có sự khác biệt giữa các môi trường thử nghiệm.
