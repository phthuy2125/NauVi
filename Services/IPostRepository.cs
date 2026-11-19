using FoodWiseAi.Api.Models;

namespace FoodWiseAi.Api.Services;

/// <summary>
/// Giao diện thao tác CRUD với bài đăng (collection "posts").
/// </summary>
public interface IPostRepository
{
    Task AddAsync(Post post);
    Task<Post?> GetByIdAsync(string id);
    Task<IReadOnlyList<Post>> GetByStatusAsync(PostStatus status);
    Task UpdateAsync(Post post);
    Task DeleteAsync(string id) => Task.CompletedTask;
}

