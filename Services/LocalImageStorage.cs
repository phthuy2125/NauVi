namespace FoodWiseAi.Api.Services;

/// <summary>
/// Lưu ảnh vào thư mục uploads/posts trên máy chủ.
/// Tên tệp được sinh bằng GUID để tránh path traversal.
/// </summary>
public class LocalImageStorage : IImageStorage
{
    private readonly string _uploadDir;
    private readonly string _requestPathPrefix;

    public LocalImageStorage(IWebHostEnvironment env, IConfiguration config)
    {
        // Đọc thư mục lưu ảnh từ cấu hình, mặc định "uploads/posts"
        var subDir = config["NauVi:PostImageDir"] ?? "uploads/posts";
        _uploadDir = Path.Combine(env.ContentRootPath, subDir);
        Directory.CreateDirectory(_uploadDir);
        _requestPathPrefix = "/" + subDir.Replace("\\", "/");
    }

    public async Task<string> SaveAsync(IFormFile image)
    {
        // Lấy phần mở rộng an toàn, chỉ chấp nhận jpg/png/webp
        var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            ext = ".jpg"; // fallback an toàn

        // Sinh tên file bằng GUID — KHÔNG dùng tên người dùng gửi
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(_uploadDir, fileName);

        await using var stream = File.Create(filePath);
        await image.CopyToAsync(stream);

        return $"{_requestPathPrefix}/{fileName}";
    }
}

