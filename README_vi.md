# NauVi AI - Hệ thống gợi ý món ăn theo giá thực tế (C# & ASP.NET Core)

Mình đã chuyển phần **backend Node.js/Express/MongoDB** trong file ZIP sang **ASP.NET Core Web API + MongoDB Driver**.

## Mapping file gốc -> file C#

- `food-backend/index.js` -> `Program.cs`
- `food-backend/models/*.js` -> `Models/*`
- `food-backend/routes/userRoutes.js` -> `Controllers/UsersController.cs`
- `food-backend/routes/foodRoutes.js` -> `Controllers/FoodsController.cs`
- `food-backend/routes/aiRoutes.js` -> `Controllers/AiController.cs`
- `food-backend/jobs/priceUpdater.js` -> `Jobs/PriceUpdateJob.cs` (Sử dụng Quartz)
- `food-backend/list_models.js` -> lệnh `--list-models`
- `food-backend/seeds/seed.js` -> `SeedData/foods.json` + `Services/DatabaseSeeder.cs`
- `food-backend/seeds/ingredientSeeder.js` -> `SeedData/ingredients.json` + `Services/DatabaseSeeder.cs`

## Endpoint đã giữ tương đương

### Users
- `POST /api/users/register`
- `POST /api/users/login`
- `POST /api/users/google-login`
- `GET /api/users/profile/{userId}`
- `PUT /api/users/update-preferences`
- `PUT /api/users/update-password`
- `POST /api/users/update-avatar`

### Foods
- `GET /api/foods/recommend`
- `GET /api/foods/all`
- `GET /api/foods/realtime-prices` (SSE)
- `POST /api/foods`
- `POST /api/foods/ai-match`
- `GET /api/foods/ingredients`
- `POST /api/foods/suggest-by-ingredients`
- `POST /api/foods/predict-image` (Mới: Nhận diện món ăn từ hình ảnh qua AI)

### AI
- `POST /api/ai/chat`
- `POST /api/ai/suggest-from-ingredients`

## Seed dữ liệu
- 50 món ăn đã được chuyển sang `SeedData/foods.json`
- 34 nguyên liệu đã được chuyển sang `SeedData/ingredients.json`

## Cách chạy

### 1. Dịch vụ Python AI (Nhận diện món ăn):
```bash
cd ai_service
python -m venv venv
.\venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --host 127.0.0.1 --port 8001 --reload
```

### 2. Backend ASP.NET Core:
```bash
dotnet restore
dotnet run
```

## Kiểm thử tự động (Testing)

- **Backend xUnit:**
```bash
dotnet test
```

- **Python AI Pytest:**
```bash
cd ai_service
pytest
```

## Lệnh tiện ích tương đương file JS cũ

```bash
dotnet run -- --seed-all
dotnet run -- --seed-foods
dotnet run -- --seed-ingredients
dotnet run -- --seed-copilot (Lấy giá thực từ Microsoft Copilot)
dotnet run -- --list-models
```

## Cấu hình cần có

- `GEMINI_API_KEY` (Đã có sẵn trong appsettings.json)
- MongoDB chạy tại `mongodb://127.0.0.1:27017`
- hoặc sửa trong `appsettings.json`

## Ghi chú quan trọng

Dự án đã được làm sạch, xóa các file debug, log và các ví dụ không cần thiết để tập trung vào phần API Backend.

Các chức năng chính:
1. **Quản lý User**: Đăng ký, đăng nhập, phân quyền Admin/User.
2. **Gợi ý món ăn**: Theo khẩu vị (personalized) và theo nguyên liệu.
3. **AI Chat**: Hỗ trợ nấu ăn qua Gemini 2.0 Flash.
4. **Đồng bộ giá**: Tự động cập nhật giá từ Bách Hóa Xanh & Co.op Mart hàng ngày qua Quartz Job.
