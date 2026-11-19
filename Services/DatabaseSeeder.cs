using System.Text.Json;
using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.Models;
using MongoDB.Driver;

namespace FoodWiseAi.Api.Services;

public sealed class DatabaseSeeder
{
    private readonly MongoDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public DatabaseSeeder(MongoDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    public async Task SeedAllAsync()
    {
        await SeedIngredientsAsync();
        await SeedFoodsAsync();
        await SeedPostsAsync();
    }

    public async Task SeedFoodsAsync()
    {
        var items = await ReadJsonAsync<List<Food>>("foods.json");
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            var existing = await _dbContext.Foods.Find(x => x.Name == item.Name).FirstOrDefaultAsync();
            if (existing == null)
            {
                item.Id = null;
                item.CreatedAt = now;
                item.UpdatedAt = now;
                item.CurrentPrice ??= new CurrentPriceInfo();
                item.CurrentPrice.UpdatedAt = now;
                await _dbContext.Foods.InsertOneAsync(item);
            }
            else
            {
                // Update existing item while preserving ID
                item.Id = existing.Id;
                item.CreatedAt = existing.CreatedAt;
                item.UpdatedAt = now;
                item.CurrentPrice ??= existing.CurrentPrice ?? new CurrentPriceInfo();
                item.CurrentPrice.UpdatedAt = now;
                await _dbContext.Foods.ReplaceOneAsync(x => x.Id == existing.Id, item);
            }
        }
    }

    public async Task SeedIngredientsAsync()
    {
        var items = await ReadJsonAsync<List<Ingredient>>("ingredients.json");
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            var existing = await _dbContext.Ingredients.Find(x => x.Name == item.Name).FirstOrDefaultAsync();
            if (existing == null)
            {
                item.Id = null;
                item.CreatedAt = now;
                item.UpdatedAt = now;
                item.CurrentPrice ??= new CurrentPriceInfo();
                    // Nếu không có ảnh sơ chế riêng, dùng ảnh chính làm ảnh sơ chế
                    if (string.IsNullOrWhiteSpace(item.PrepImageUrl)) item.PrepImageUrl = item.ImageUrl;
                if (item.CurrentPrice.Coopmart == 0) item.CurrentPrice.Coopmart = item.BasePrice.Coopmart;
                if (item.CurrentPrice.Bachhoaxanh == 0) item.CurrentPrice.Bachhoaxanh = item.BasePrice.Bachhoaxanh;
                item.CurrentPrice.UpdatedAt = now;
                await _dbContext.Ingredients.InsertOneAsync(item);
            }
            else
            {
                // Preserve ID to maintain relationships
                item.Id = existing.Id;
                item.CreatedAt = existing.CreatedAt;
                item.UpdatedAt = now;
                item.CurrentPrice ??= existing.CurrentPrice ?? new CurrentPriceInfo();
                    // Preserve existing prep image if present; otherwise fallback to existing image or new image
                    if (string.IsNullOrWhiteSpace(item.PrepImageUrl))
                        item.PrepImageUrl = existing.PrepImageUrl ?? existing.ImageUrl ?? item.ImageUrl;
                if (item.CurrentPrice.Coopmart == 0) item.CurrentPrice.Coopmart = item.BasePrice.Coopmart;
                if (item.CurrentPrice.Bachhoaxanh == 0) item.CurrentPrice.Bachhoaxanh = item.BasePrice.Bachhoaxanh;
                item.CurrentPrice.UpdatedAt = now;
                await _dbContext.Ingredients.ReplaceOneAsync(x => x.Id == existing.Id, item);
            }
        }
    }

    public async Task SeedPostsAsync()
    {
        // Lấy hoặc tạo tài khoản người dùng mẫu để gán tác giả bài viết
        var author = await _dbContext.Users.Find(u => u.Username == "bepviet_ngon").FirstOrDefaultAsync();
        if (author == null)
        {
            author = new User
            {
                Username = "bepviet_ngon",
                Password = "Password@123",
                Email = "bepviet@nauvi.vn",
                Role = "user",
                Avatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _dbContext.Users.InsertOneAsync(author);
        }

        // Lấy danh sách món ăn thật từ database
        var allFoods = await _dbContext.Foods.Find(Builders<Food>.Filter.Empty).ToListAsync();
        if (allFoods.Count == 0) return;

        Food FindFood(string keyword) =>
            allFoods.FirstOrDefault(f => f.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ?? allFoods[0];

        var phoBo = FindFood("Phở");
        var bunBo = FindFood("Bún Bò");
        var banhXeo = FindFood("Bánh Xèo");
        var comChien = FindFood("Cơm Chiên");
        var goiCuon = FindFood("Gỏi Cuốn");

        var samplePosts = new List<Post>
        {
            new()
            {
                UserId = author.Id ?? "user-demo-1",
                FoodId = phoBo.Id ?? string.Empty,
                ImageUrl = string.IsNullOrWhiteSpace(phoBo.ImageUrl) ? "https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=800" : phoBo.ImageUrl,
                Content = "Cuối tuần tự tay hầm nồi nước dùng phở bò hơn 6 tiếng, nước ngọt thanh tự nhiên từ xương bò và hoa hồi quế. Cả nhà ai cũng khen ngon hơn ngoài hàng!",
                Status = PostStatus.Approved,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                UserId = author.Id ?? "user-demo-1",
                FoodId = bunBo.Id ?? string.Empty,
                ImageUrl = string.IsNullOrWhiteSpace(bunBo.ImageUrl) ? "https://images.unsplash.com/photo-1594998893017-36147cbcae05?w=800" : bunBo.ImageUrl,
                Content = "Lần đầu thử nấu bún bò Huế chuẩn vị miền Trung, thêm chút mắm ruốc xào sả ớt cay nồng cực đã. Nấu theo công thức của NauVi thành công ngay lần đầu!",
                Status = PostStatus.Approved,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                UserId = author.Id ?? "user-demo-1",
                FoodId = banhXeo.Id ?? string.Empty,
                ImageUrl = string.IsNullOrWhiteSpace(banhXeo.ImageUrl) ? "https://images.unsplash.com/photo-1627308595229-7830a5c91f9f?w=800" : banhXeo.ImageUrl,
                Content = "Bánh xèo giòn rụm viền mỏng vàng ươm, nhân tôm thịt đầy đặn, cuốn kèm rau cải non và nước mắm chua ngọt tuyệt đỉnh ngày mưa.",
                Status = PostStatus.Approved,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                UserId = author.Id ?? "user-demo-1",
                FoodId = comChien.Id ?? string.Empty,
                ImageUrl = string.IsNullOrWhiteSpace(comChien.ImageUrl) ? "https://images.unsplash.com/photo-1603133872878-684f208fb84b?w=800" : comChien.ImageUrl,
                Content = "Bữa trưa làm nhanh đĩa cơm chiên dưa bò dưa cải chua giòn, thịt bò mềm thơm hạt cơm tơi xốp, vừa tiết kiệm vừa ngon miệng.",
                Status = PostStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddHours(-4),
                UpdatedAt = DateTime.UtcNow.AddHours(-4)
            },
            new()
            {
                UserId = author.Id ?? "user-demo-1",
                FoodId = goiCuon.Id ?? string.Empty,
                ImageUrl = string.IsNullOrWhiteSpace(goiCuon.ImageUrl) ? "https://images.unsplash.com/photo-1541544741938-0af808871cc0?w=800" : goiCuon.ImageUrl,
                Content = "Gỏi cuốn tôm thịt thanh đạm, chấm kèm tương bơ đậu phộng béo bùi chống ngấy sau những ngày tiệc tùng nhiều dầu mỡ.",
                Status = PostStatus.Hidden,
                CreatedAt = DateTime.UtcNow.AddHours(-8),
                UpdatedAt = DateTime.UtcNow.AddHours(-8)
            }
        };

        foreach (var post in samplePosts)
        {
            var existing = await _dbContext.Posts.Find(p => p.Content == post.Content).FirstOrDefaultAsync();
            if (existing == null)
            {
                await _dbContext.Posts.InsertOneAsync(post);
            }
        }
    }

    private async Task<T> ReadJsonAsync<T>(string fileName)
    {
        var path = Path.Combine(_environment.ContentRootPath, "SeedData", fileName);
        await using var stream = File.OpenRead(path);
        var data = await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data is null)
        {
            throw new InvalidOperationException($"Không đọc được dữ liệu seed từ {fileName}.");
        }

        return data;
    }
}
