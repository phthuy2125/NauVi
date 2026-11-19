namespace FoodWiseAi.Api.Models;

/// <summary>
/// Dữ liệu gửi lên khi tạo bài đăng mới (multipart/form-data).
/// </summary>
public class CreatePostRequest
{
    public string? FoodId { get; set; }
    public string? Content { get; set; }
    public IFormFile? Image { get; set; }
}

/// <summary>
/// Dữ liệu gửi lên khi thêm bình luận vào bài đăng.
/// </summary>
public class AddPostCommentRequest
{
    public string? Content { get; set; }
}

