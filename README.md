# NauVi
Tên đề tài (VN):Phát triển NauVi - Nền tảng khám phá và chia sẻ món ăn nhận diện món Việt từ ảnh dựa trên học chuyển giao.

Tên đề tài (Eng):Developing NauVi - A Food Discovery and Sharing Platform Recognizing Vietnamese Dishes from Photos Based on Transfer Learning.

“NauVi giải quyết vấn đề tìm kiếm công thức từ hình ảnh món ăn và chia sẻ thông tin ẩm thực phân tán trên nhiều nền tảng cho người yêu thích ẩm thực, hướng tới giảm ít nhất 30% thời gian tìm công thức so với cách tìm kiếm thông thường.”

## Chỉ số đánh giá

| Chỉ số | Cách đo | Trước | Sau (mục tiêu) | Ngày đo | Bằng chứng |
|---|---|---|---|---|---|
| **Top-3 accuracy** của mô hình nhận diện món ăn | Chạy mô hình trên tập ảnh kiểm thử đã gán nhãn; tính tỉ lệ ảnh có nhãn đúng nằm trong 3 dự đoán có xác suất cao nhất | Chưa đo (baseline: mô hình cơ sở) | --- | W1 - W2 và W8 | Bảng kết quả đánh giá trên tập kiểm thử, confusion matrix, nhật ký chạy mô hình |
| **Thời gian tìm thông tin và công thức từ ảnh món ăn** | Bấm giờ từ lúc bắt đầu tìm đến khi tìm được công thức phù hợp | ~35 giấy | Giảm dự kiến ít nhất 30% so với trước | W1 - W2 và W8 | Bảng ghi kết quả thử nghiệm |
--

## 🚀 Hướng dẫn cài đặt và chạy dự án

### Yêu cầu hệ thống

| Công cụ | Phiên bản tối thiểu | Link tải |
|---------|-------------------|----------|
| **.NET SDK** | 8.0+ | https://dotnet.microsoft.com/download |
| **MongoDB** | 6.0+ | https://www.mongodb.com/try/download/community |
| **Git** | Bất kỳ | https://git-scm.com |

### 1. Clone dự án

```bash
git clone https://github.com/phthuy2125/NauVi.git
cd NauVi
```

### 2. Cấu hình API Key

Tạo file `.env` trong thư mục gốc của dự án (cùng cấp với `FoodWiseAi.csproj`):

```
GEMINI_API_KEY=your_gemini_api_key_here
```

> **Lấy Gemini API Key miễn phí tại:** https://aistudio.google.com/apikey

### 3. Cấu hình cơ sở dữ liệu

Sao chép file cấu hình mẫu:

```bash
copy appsettings.example.json appsettings.json
```

Mở `appsettings.json` và chỉnh sửa nếu cần:

```json
{
  "MongoSettings": {
    "ConnectionString": "mongodb://127.0.0.1:27017",
    "DatabaseName": "food_system"
  }
}
```

> Đảm bảo MongoDB đang chạy trên máy (`mongod` hoặc qua MongoDB Compass).

### 4. Khởi tạo dữ liệu mẫu (tuỳ chọn)

```bash
dotnet run -- --seed-all
```

### 5. Chạy ứng dụng

```bash
dotnet run
```

Ứng dụng sẽ khởi động tại: **http://localhost:5062**

---
