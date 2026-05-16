using MongoDB.Bson.Serialization.Attributes;

namespace FoodWiseAi.Api.Models;

[BsonIgnoreExtraElements]
public sealed class Ingredient : MongoEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("basePrice")]
    public PricePair BasePrice { get; set; } = new();

    [BsonElement("currentPrice")]
    public CurrentPriceInfo CurrentPrice { get; set; } = new();

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("prepImageUrl")]
    public string? PrepImageUrl { get; set; }

    [BsonElement("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    [BsonElement("unit")]
    public string Unit { get; set; } = string.Empty;
}
