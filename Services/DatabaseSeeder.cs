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
