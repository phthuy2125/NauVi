namespace FoodWiseAi.Api.Data;

public sealed class MongoSettings
{
    public string ConnectionString { get; set; } = "mongodb://127.0.0.1:27017";
    public string DatabaseName { get; set; } = "food_system";
}
