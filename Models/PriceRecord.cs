using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FoodWiseAi.Api.Models;

/// <summary>
/// Bản ghi giá nguyên liệu được crawl từ các API giao đồ ăn (ShopeeFood).
/// Lưu vào collection "price_records" trong MongoDB.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class PriceRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    [BsonElement("originalPrice")]
    public decimal OriginalPrice { get; set; }

    [BsonElement("actualPrice")]
    public decimal ActualPrice { get; set; }

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("crawledAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CrawledAt { get; set; } = DateTime.UtcNow;
}
