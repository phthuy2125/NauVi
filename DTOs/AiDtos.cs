namespace FoodWiseAi.Api.DTOs;

public sealed class ChatRequest
{
    public string? UserId { get; set; }
    public string? Message { get; set; }
    public List<ChatHistoryItem>? History { get; set; }
}

public sealed class ChatHistoryItem
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = string.Empty;
}

public sealed class SuggestFromIngredientsRequest
{
    public List<string>? SelectedIngredients { get; set; }
}
