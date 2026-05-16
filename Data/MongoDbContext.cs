using FoodWiseAi.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FoodWiseAi.Api.Data;

public sealed class MongoDbContext
{
    public MongoDbContext(IOptions<MongoSettings> settings)
    {
        var value = settings.Value;
        var client = new MongoClient(value.ConnectionString);
        Database = client.GetDatabase(value.DatabaseName);
        Users = Database.GetCollection<User>("users");
        Foods = Database.GetCollection<Food>("foods");
        Ingredients = Database.GetCollection<Ingredient>("ingredients");
    }

    public IMongoDatabase Database { get; }
    public IMongoCollection<User> Users { get; }
    public IMongoCollection<Food> Foods { get; }
    public IMongoCollection<Ingredient> Ingredients { get; }
}
