using FoodWiseAi.Api.Models;

namespace FoodWiseAi.Api.Services;

/// <summary>
/// Giao diện tra cứu thông tin món ăn, hỗ trợ tách rời CSDL khi viết unit test.
/// </summary>
public interface IFoodService
{
    Task<Food?> GetByIdAsync(string id);
}
