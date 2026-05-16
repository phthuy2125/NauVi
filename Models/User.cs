using MongoDB.Bson.Serialization.Attributes;

namespace FoodWiseAi.Api.Models;

[BsonIgnoreExtraElements]
public sealed class User : MongoEntity
{
    [BsonElement("username")]
    public string? Username { get; set; }

    [BsonElement("password")]
    public string? Password { get; set; }

    [BsonElement("googleId")]
    public string? GoogleId { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("role")]
    public string Role { get; set; } = "user";

    [BsonElement("preferences")]
    public UserPreferences Preferences { get; set; } = new();

    [BsonElement("avatar")]
    public string Avatar { get; set; } = string.Empty;

    [BsonElement("favoriteFoodIds")]
    public List<string> FavoriteFoodIds { get; set; } = new();

    [BsonElement("cookingHistory")]
    public List<UserHistoryItem> CookingHistory { get; set; } = new();
}
