using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FoodWiseAi.Api.Models;

[BsonIgnoreExtraElements]
public abstract class MongoEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PricePair
{
    [BsonElement("coopmart")]
    public decimal Coopmart { get; set; }

    [BsonElement("bachhoaxanh")]
    public decimal Bachhoaxanh { get; set; }
}

public sealed class CurrentPriceInfo : PricePair
{
    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("priceSource")]
    public string PriceSource { get; set; } = "GeminiKnowledge"; // "GoogleGrounding" hoặc "GeminiKnowledge"
}

public sealed class PriceHistoryItem
{
    [BsonElement("price")]
    public decimal Price { get; set; }

    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    [BsonElement("recordedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public sealed class UserPreferences
{
    [BsonElement("spicy")]
    public int Spicy { get; set; } = 3;

    [BsonElement("sweet")]
    public int Sweet { get; set; } = 3;

    [BsonElement("salty")]
    public int Salty { get; set; } = 3;

    [BsonElement("sour")]
    public int Sour { get; set; } = 3;

    [BsonElement("umami")]
    public int Umami { get; set; } = 3;

    [BsonElement("texture")]
    public string Texture { get; set; } = "nuoc";

    [BsonElement("organic")]
    public bool Organic { get; set; } = false;

    [BsonElement("light")]
    public bool Light { get; set; } = false;
}

public class UserHistoryItem
{
    [BsonElement("foodId")]
    public string FoodId { get; set; } = string.Empty;

    [BsonElement("completedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
