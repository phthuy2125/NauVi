using MongoDB.Bson.Serialization.Attributes;

namespace FoodWiseAi.Api.Models;

/// <summary>
/// Trạng thái duyệt bài đăng.
/// </summary>
public enum PostStatus
{
    /// <summary>Chờ duyệt</summary>
    Pending = 0,

    /// <summary>Đã duyệt — hiển thị công khai</summary>
    Approved = 1,

    /// <summary>Bị từ chối — ẩn khỏi danh sách công khai</summary>
    Hidden = 2
}

/// <summary>
/// Bài đăng chia sẻ ảnh món ăn của người dùng.
/// </summary>
[BsonIgnoreExtraElements]
public class Post : MongoEntity
{
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("foodId")]
    public string FoodId { get; set; } = string.Empty;

    [BsonElement("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("status")]
    public PostStatus Status { get; set; } = PostStatus.Pending;

    [BsonElement("authorName")]
    public string? AuthorName { get; set; }

    [BsonElement("authorAvatar")]
    public string? AuthorAvatar { get; set; }

    [BsonElement("likes")]
    public List<string> Likes { get; set; } = new();

    [BsonElement("comments")]
    public List<PostComment> Comments { get; set; } = new();
}

public class PostComment
{
    [BsonElement("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("avatar")]
    public string? Avatar { get; set; }

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

