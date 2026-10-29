using MongoDB.Bson.Serialization.Attributes;

namespace FoodWiseAi.Api.Models;

[BsonIgnoreExtraElements]
public sealed class PredictionLog : MongoEntity
{
    [BsonElement("userId")]
    public string? UserId { get; set; }

    [BsonElement("fileName")]
    public string? FileName { get; set; }

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("top3")]
    public List<PredictionFoodItem> Top3 { get; set; } = new();

    [BsonElement("confidence")]
    public double Confidence { get; set; }

    [BsonElement("modelVersion")]
    public string ModelVersion { get; set; } = string.Empty;
}

public sealed class PredictionFoodItem
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("confidence")]
    public double Confidence { get; set; }

    [BsonElement("foodId")]
    public string? FoodId { get; set; }
}
