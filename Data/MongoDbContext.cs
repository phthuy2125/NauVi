using FoodWiseAi.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FoodWiseAi.Api.Data;

public class MongoDbContext
{
    public MongoDbContext() { }

    public MongoDbContext(IOptions<MongoSettings> settings)
    {
        var value = settings.Value;
        var client = new MongoClient(value.ConnectionString);
        Database = client.GetDatabase(value.DatabaseName);
        Users = Database.GetCollection<User>("users");
        Foods = Database.GetCollection<Food>("foods");
        Ingredients = Database.GetCollection<Ingredient>("ingredients");
        PredictionLogs = Database.GetCollection<PredictionLog>("prediction_logs");
    }

    public virtual IMongoDatabase Database { get; } = null!;
    public virtual IMongoCollection<User> Users { get; } = null!;
    public virtual IMongoCollection<Food> Foods { get; } = null!;
    public virtual IMongoCollection<Ingredient> Ingredients { get; } = null!;
    public virtual IMongoCollection<PredictionLog> PredictionLogs { get; } = null!;
}
