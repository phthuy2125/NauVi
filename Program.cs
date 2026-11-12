using System.Text;
using MongoDB.Driver;
using FoodWiseAi.Api.Models;

using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

// ─── Đọc file .env (nếu có) và nạp vào biến môi trường ───────────────────────
var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
        var eqIdx = trimmed.IndexOf('=');
        if (eqIdx < 1) continue;
        var key = trimmed[..eqIdx].Trim();
        var value = trimmed[(eqIdx + 1)..].Trim();
        Environment.SetEnvironmentVariable(key, value);
    }
}
// ──────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// Fix lỗi hiển thị tiếng Việt trên Console Windows
Console.OutputEncoding = System.Text.Encoding.UTF8;

builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoSettings"));
builder.Services.Configure<SmtpSettings>(options =>
{
    builder.Configuration.GetSection("SmtpSettings").Bind(options);

    // Hỗ trợ đọc từ biến môi trường hoặc .env nếu có
    var envUser = Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? Environment.GetEnvironmentVariable("SMTP_USER");
    if (!string.IsNullOrWhiteSpace(envUser)) options.UserName = envUser;

    var envPass = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? Environment.GetEnvironmentVariable("SMTP_PASS");
    if (!string.IsNullOrWhiteSpace(envPass)) options.Password = envPass;

    var envSender = Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL");
    if (!string.IsNullOrWhiteSpace(envSender)) options.SenderEmail = envSender;
});
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<GoogleAuthService>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<CopilotService>();
builder.Services.AddHttpClient<IFoodImagePredictionService, FoodImagePredictionService>();

// Đăng ký dịch vụ tra cứu món ăn & gửi email công thức
builder.Services.AddScoped<IFoodService, FoodService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IRecipeEmailRateLimiter, RecipeEmailRateLimiter>();

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
