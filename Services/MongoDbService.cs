using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.Models;
using MongoDB.Driver;
using System.Text.Json;

namespace FoodWiseAi.Api.Services;

/// <summary>
/// Service quản lý dữ liệu giá thị trường trong MongoDB.
/// Hỗ trợ đồng bộ giá thực tế từ Google Search Grounding vào hệ thống.
/// </summary>
public class MongoDbService
{
    private readonly IMongoCollection<PriceRecord> _priceRecords;
    private readonly IMongoCollection<Ingredient> _ingredients;
    private readonly IMongoCollection<Food> _foods;
    private readonly ILogger<MongoDbService> _logger;

    public MongoDbService(MongoDbContext dbContext, ILogger<MongoDbService> logger)
    {
        _logger = logger;
        _priceRecords = dbContext.Database.GetCollection<PriceRecord>("price_records");
        _ingredients = dbContext.Ingredients;
        _foods = dbContext.Foods;
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        var indexKeys = Builders<PriceRecord>.IndexKeys
            .Ascending(r => r.ProductName)
            .Ascending(r => r.Source)
            .Ascending(r => r.CrawledAt);

        var indexOptions = new CreateIndexOptions { Name = "idx_product_source_date" };
        _priceRecords.Indexes.CreateOne(new CreateIndexModel<PriceRecord>(indexKeys, indexOptions));
    }

    /// <summary>
    /// Lưu danh sách giá vào MongoDB (Upsert dựa trên Ngày + Tên + Nguồn).
    /// </summary>
    public async Task SavePricesAsync(List<PriceRecord> records, CancellationToken ct = default)
    {
        foreach (var record in records)
        {
            try
            {
                var dayStart = record.CrawledAt.Date;
                var dayEnd = dayStart.AddDays(1);

                var filter = Builders<PriceRecord>.Filter.And(
                    Builders<PriceRecord>.Filter.Eq(r => r.ProductName, record.ProductName),
                    Builders<PriceRecord>.Filter.Eq(r => r.Source, record.Source),
                    Builders<PriceRecord>.Filter.Gte(r => r.CrawledAt, dayStart),
                    Builders<PriceRecord>.Filter.Lt(r => r.CrawledAt, dayEnd)
                );

                var update = Builders<PriceRecord>.Update
                    .Set(r => r.OriginalPrice, record.OriginalPrice)
                    .Set(r => r.ActualPrice, record.ActualPrice)
                    .Set(r => r.Category, record.Category)
                    .Set(r => r.CrawledAt, record.CrawledAt);

                await _priceRecords.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi upsert bản ghi: {Name} từ {Source}", record.ProductName, record.Source);
            }
        }
    }

    /// <summary>
    /// Lấy bản ghi giá mới nhất cho từ khóa.
    /// </summary>
    public async Task<List<PriceRecord>> GetLatestAsync(string keyword)
    {
        var filter = Builders<PriceRecord>.Filter.Regex(
            r => r.ProductName, 
            new MongoDB.Bson.BsonRegularExpression(keyword, "i")
        );

        var rawRecords = await _priceRecords.Find(filter)
                                            .SortByDescending(r => r.CrawledAt)
                                            .ToListAsync();

        var latestRecords = rawRecords
            .GroupBy(r => new { r.ProductName, r.Source })
            .Select(g => g.First())
            .ToList();

        return latestRecords;
    }

    /// <summary>
    /// Đồng bộ giá thực tế từ các bản ghi cào được vào thực thể Ingredient và Food.
    /// <summary>
    /// Logic: Tìm tất cả nguyên liệu mà tên của chúng xuất hiện trong 'productName' (Ví dụ: "Thịt bò" xuất hiện trong "Thịt đùi bò (kg)").
    /// </summary>
    public async Task SyncPricesToMainEntitiesAsync(string productName, List<PriceRecord> bhx, List<PriceRecord> coop, string priceSource = "GeminiKnowledge", CancellationToken ct = default)
    {
        var bhxPrice = bhx.Any() ? bhx.Min(r => r.ActualPrice) : 0;
        var coopPrice = coop.Any() ? coop.Min(r => r.ActualPrice) : 0;

        if (bhxPrice <= 0 && coopPrice <= 0) return;

        var now = DateTime.UtcNow;

        // 1. Tìm và cập nhật Ingredients dựa trên từ khóa chứa trong ProductName
        // Lấy tất cả ingredients để so khớp cục bộ
        var allIngredients = await _ingredients.Find(Builders<Ingredient>.Filter.Empty).ToListAsync(ct);
        
        var matchedIngredients = allIngredients.Where(ing => 
        {
            // Tách các từ trong tên nguyên liệu (ví dụ: "Thịt bò" -> ["Thịt", "bò"])
            var words = ing.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Kiểm tra xem tất cả các từ này có xuất hiện trong productName không
            return words.All(w => productName.Contains(w, StringComparison.OrdinalIgnoreCase));
        }).ToList();

        foreach (var ing in matchedIngredients)
        {
            var update = Builders<Ingredient>.Update
                .Set("currentPrice.bachhoaxanh", bhxPrice)
                .Set("currentPrice.coopmart", coopPrice)
                .Set("currentPrice.updatedAt", now)
                .Set("currentPrice.priceSource", priceSource);
            await _ingredients.UpdateOneAsync(i => i.Id == ing.Id, update, cancellationToken: ct);
            _logger.LogInformation("  -> Đã cập nhật giá cho Ingredient: {Name} (Source: {Source})", ing.Name, priceSource);
        }

        // 2. Cập nhật bảng Foods (những món có tên chứa keyword)
        var foodFilter = Builders<Food>.Filter.Regex(f => f.Name, new MongoDB.Bson.BsonRegularExpression(productName, "i"));
        var foodUpdate = Builders<Food>.Update
            .Set("currentPrice.bachhoaxanh", bhxPrice)
            .Set("currentPrice.coopmart", coopPrice)
            .Set("currentPrice.updatedAt", now)
            .Set("currentPrice.priceSource", priceSource);
        await _foods.UpdateManyAsync(foodFilter, foodUpdate, cancellationToken: ct);
    }



    /// <summary>
    /// Cho phép nhập dữ liệu JSON trực tiếp (ví dụ lấy từ Google AI Studio).
    /// </summary>
    public async Task SeedFromJsonFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Không tìm thấy file: {Path}", filePath);
            return;
        }

        var json = await File.ReadAllTextAsync(filePath, ct);
        var records = JsonSerializer.Deserialize<List<PriceRecord>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (records != null && records.Any())
        {
            // 1. Lưu tất cả bản ghi giá vào collection history
            var now = DateTime.UtcNow;
            foreach (var r in records) r.CrawledAt = now;
            await SavePricesAsync(records, ct);
            _logger.LogInformation("✅ Đã nhập {count} bản ghi từ file JSON.", records.Count);

            // 2. Đồng bộ vào bảng Ingredients & Foods
            var allIngredients = await _ingredients.Find(Builders<Ingredient>.Filter.Empty).ToListAsync(ct);
            
            foreach (var ing in allIngredients)
            {
                // Tìm tất cả các bản ghi giá (từ bất kỳ ProductName nào) mà có chứa tên nguyên liệu này
                // Dùng logic so khớp từng từ để đảm bảo độ chính xác cao nhất (Ví dụ: "Thịt bò" khớp với "Thịt đùi bò")
                var relatedRecords = records.Where(r => 
                {
                    var words = ing.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return words.All(w => r.ProductName.Contains(w, StringComparison.OrdinalIgnoreCase));
                }).ToList();
                
                if (relatedRecords.Any())
                {
                    var bhx = relatedRecords.Where(r => r.Source.Contains("BachHoaXanh", StringComparison.OrdinalIgnoreCase)).ToList();
                    var coop = relatedRecords.Where(r => r.Source.Contains("CoopMart", StringComparison.OrdinalIgnoreCase) || r.Source.Contains("Co.op", StringComparison.OrdinalIgnoreCase)).ToList();
                    
                    // YÊU CẦU: Phải có đủ 2 sàn mới cập nhật
                    if (bhx.Any() && coop.Any())
                    {
                        await SyncPricesToMainEntitiesAsync(ing.Name, bhx, coop, "GeminiKnowledge", ct);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Bỏ qua '{Name}' vì thiếu giá từ một trong hai sàn (BHX: {bCount}, Coop: {cCount})", 
                            ing.Name, bhx.Count, coop.Count);
                    }
                }
            }
        }
    }
}
