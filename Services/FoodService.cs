using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.Models;
using MongoDB.Driver;

namespace FoodWiseAi.Api.Services;

/// <summary>
/// Triển khai dịch vụ tra cứu món ăn từ collection 'foods' trong MongoDB.
/// </summary>
public class FoodService : IFoodService
{
    private readonly MongoDbContext _dbContext;

    public FoodService(MongoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Food?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        try
        {
            return await _dbContext.Foods
                .Find(x => x.Id == id)
                .FirstOrDefaultAsync();
        }
        catch
        {
            // Trả về null khi Id không hợp lệ (ví dụ lỗi định dạng ObjectId)
            return null;
        }
    }
}
