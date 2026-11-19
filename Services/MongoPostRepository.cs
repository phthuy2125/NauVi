using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.Models;
using MongoDB.Driver;

namespace FoodWiseAi.Api.Services;

/// <summary>
/// Triển khai IPostRepository sử dụng MongoDB collection "posts".
/// </summary>
public class MongoPostRepository : IPostRepository
{
    private readonly IMongoCollection<Post> _posts;

    public MongoPostRepository(MongoDbContext dbContext)
    {
        _posts = dbContext.Database.GetCollection<Post>("posts");
    }

    public async Task AddAsync(Post post)
    {
        await _posts.InsertOneAsync(post);
    }

    public async Task<Post?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try
        {
            return await _posts.Find(p => p.Id == id).FirstOrDefaultAsync();
        }
        catch
        {
            // Id không hợp lệ (sai ObjectId format)
            return null;
        }
    }

    public async Task<IReadOnlyList<Post>> GetByStatusAsync(PostStatus status)
    {
        return await _posts
            .Find(p => p.Status == status)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateAsync(Post post)
    {
        var update = Builders<Post>.Update
            .Set(p => p.Status, post.Status)
            .Set(p => p.Content, post.Content)
            .Set(p => p.Likes, post.Likes)
            .Set(p => p.Comments, post.Comments)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _posts.UpdateOneAsync(p => p.Id == post.Id, update);
    }

    public async Task DeleteAsync(string id)
    {
        await _posts.DeleteOneAsync(p => p.Id == id);
    }
}

