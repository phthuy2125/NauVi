using Microsoft.AspNetCore.Http;

namespace FoodWiseAi.Api.DTOs;

public sealed class FoodCreateForm
{
    public string? Name { get; set; }
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? Taste { get; set; }
    public string? Type { get; set; }
    public string? Calories { get; set; }
    public string? Ingredients { get; set; }
    public string? Category { get; set; }
    public string? Restaurant { get; set; }
    public string? Tags { get; set; }
    public string? BasePriceCoop { get; set; }
    public string? BasePriceBHX { get; set; }
    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
}

public sealed class FoodAiMatchRequest
{
    public FoodCriteria? Criteria { get; set; }
}

public sealed class FoodCriteria
{
    public List<string>? Ingredients { get; set; }
    public List<string>? Taste { get; set; }
    public string? Diet { get; set; }
    public string? Consistency { get; set; }
    public string? Carbs { get; set; }
}

public sealed class SuggestByIngredientsRequest
{
    public List<string>? SelectedIngredientNames { get; set; }
}
