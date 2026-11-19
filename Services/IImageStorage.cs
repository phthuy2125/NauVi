namespace FoodWiseAi.Api.Services;

/// <summary>
/// Lưu ảnh tải lên và trả về đường dẫn công khai.
/// </summary>
public interface IImageStorage
{
    Task<string> SaveAsync(IFormFile image);
}

