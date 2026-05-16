using MongoDB.Bson.Serialization.Attributes;

namespace FoodWiseAi.Api.Models;

[BsonIgnoreExtraElements]
public sealed class Food : MongoEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("nameEn")]
    public string? NameEn { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("taste")]
    public List<string> Taste { get; set; } = new();

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("calories")]
    public int Calories { get; set; }

    [BsonElement("ingredients")]
    public List<string> Ingredients { get; set; } = new();

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("prepImageUrl")]
    public string? PrepImageUrl { get; set; }

    [BsonElement("restaurant")]
    public string? Restaurant { get; set; }

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    [BsonElement("basePrice")]
    public PricePair BasePrice { get; set; } = new();

    [BsonElement("currentPrice")]
    public CurrentPriceInfo CurrentPrice { get; set; } = new();

    [BsonElement("priceHistory")]
    public List<PriceHistoryItem> PriceHistory { get; set; } = new();

    [BsonElement("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    [BsonElement("rating")]
    public decimal Rating { get; set; }

    [BsonElement("reviewCount")]
    public int ReviewCount { get; set; }

    [BsonElement("comments")]
    public List<FoodComment> Comments { get; set; } = new();

    // Tính toán động - không lưu vào DB
    public int? MatchPercent { get; set; }
}

public class FoodComment
{
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [BsonElement("rating")]
    public int Rating { get; set; }

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
