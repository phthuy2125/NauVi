using System.Text;
using MongoDB.Driver;
using FoodWiseAi.Api.Models;

using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Fix lỗi hiển thị tiếng Việt trên Console Windows
Console.OutputEncoding = System.Text.Encoding.UTF8;

builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoSettings"));
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<GoogleAuthService>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<CopilotService>();
builder.Services.AddHttpClient<IFoodImagePredictionService, FoodImagePredictionService>();

builder.Services.AddSingleton<DatabaseSeeder>();

// Service lưu vào MongoDB collection "price_records"
builder.Services.AddSingleton<MongoDbService>();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllersWithViews();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "your-secret-key-very-long-random";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

var app = builder.Build();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);

app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

if (builder.Configuration.GetValue<bool>("NauVi:SeedOnStartup")
    || builder.Configuration.GetValue<bool>("FoodWise:SeedOnStartup"))
{
    using var startupScope = app.Services.CreateScope();
    var startupSeeder = startupScope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await startupSeeder.SeedAllAsync();
}

if (args.Contains("--seed-all", StringComparer.OrdinalIgnoreCase)
    || args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAllAsync();
    return;
}

if (args.Contains("--seed-foods", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedFoodsAsync();
    return;
}

if (args.Contains("--seed-ingredients", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedIngredientsAsync();
    return;
}


if (args.Contains("--seed-copilot", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("🔍 Bắt đầu lấy giá thực từ BachHoaXanh & Coop Online qua Microsoft Copilot...");
    using var scope = app.Services.CreateScope();
    
    var copilotSvc = scope.ServiceProvider.GetRequiredService<CopilotService>();
    await copilotSvc.UpdateIngredientsJsonAsync();
    
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedIngredientsAsync();
    
    Console.WriteLine("\n✅ Hoàn tất! Dữ liệu đã được lưu vào ingredients.json và MongoDB.");
    return;
}

if (args.Contains("--list-models", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var gemini = scope.ServiceProvider.GetRequiredService<GeminiService>();
    var models = await gemini.ListModelsAsync();
    foreach (var model in models)
    {
        Console.WriteLine(model);
    }
    return;
}

app.Run();
