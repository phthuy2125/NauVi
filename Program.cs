using System.Text;
using System.Threading.RateLimiting;
using MongoDB.Driver;
using FoodWiseAi.Api.Models;
using FoodWiseAi.Api.Security;
using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
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

// Đăng ký dịch vụ bài đăng cộng đồng & lưu trữ ảnh
builder.Services.AddScoped<IPostRepository, MongoPostRepository>();
builder.Services.AddScoped<IImageStorage, LocalImageStorage>();

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

// ─── Biện pháp 1: Hỗ trợ Reverse Proxy / Forwarded Headers ────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Chú ý: Trên môi trường production thực tế, cần giới hạn KnownNetworks hoặc KnownProxies
    // để tránh bị giả mạo header X-Forwarded-For gây bypass rate limiter.
});

// ─── Biện pháp 1: Giới hạn tần suất gọi (Rate Limiting) ────────────────────────
var rateLimitSection = builder.Configuration.GetSection("RateLimiting:PredictImage");
int predictPermitLimit = rateLimitSection.GetValue<int>("PermitLimit", 10);
int predictWindowMinutes = rateLimitSection.GetValue<int>("WindowMinutes", 1);
int predictQueueLimit = rateLimitSection.GetValue<int>("QueueLimit", 0);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        if (!context.HttpContext.Response.Headers.ContainsKey("Retry-After"))
        {
            context.HttpContext.Response.Headers["Retry-After"] = (predictWindowMinutes * 60).ToString();
        }

        var payload = new
        {
            error = "Too Many Requests",
            message = "Bạn đã gửi quá nhiều yêu cầu nhận diện ảnh trong thời gian ngắn. Vui lòng thử lại sau.",
            retryAfterSeconds = predictWindowMinutes * 60
        };

        await context.HttpContext.Response.WriteAsJsonAsync(payload, cancellationToken);
    };

    options.AddPolicy("PredictImagePolicy", httpContext =>
    {
        string partitionKey;
        var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? httpContext.User?.FindFirst("sub")?.Value
                     ?? httpContext.User?.FindFirst("userId")?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            partitionKey = $"user_{userId}";
        }
        else
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
            partitionKey = $"ip_{ip}";
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = predictPermitLimit,
                Window = TimeSpan.FromMinutes(predictWindowMinutes),
                QueueLimit = predictQueueLimit,
                AutoReplenishment = true
            });
    });
});

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

app.UseForwardedHeaders();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);

app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

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

// Endpoint kiểm tra sức khỏe dịch vụ cho Docker Healthcheck
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "NauVi ASP.NET Core 8 Web",
    timestamp = DateTime.UtcNow
}));

if (builder.Configuration.GetValue<bool>("NauVi:SeedOnStartup")
    || builder.Configuration.GetValue<bool>("FoodWise:SeedOnStartup")
    || builder.Configuration.GetValue<bool>("SEED_ON_STARTUP")
    || string.Equals(Environment.GetEnvironmentVariable("SEED_ON_STARTUP"), "true", StringComparison.OrdinalIgnoreCase))
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

if (args.Contains("--seed-posts", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedPostsAsync();
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
