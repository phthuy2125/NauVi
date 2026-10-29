using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FoodWiseAi.Api.Controllers;
using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.Models;
using FoodWiseAi.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace FoodWiseAi.Tests;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responseFactory(request));
    }
}

public class FoodImagePredictionTests
{
    private readonly Mock<MongoDbContext> _mockDbContext;
    private readonly Mock<IMongoCollection<Food>> _mockFoodCollection;
    private readonly Mock<IMongoCollection<PredictionLog>> _mockLogCollection;
    private readonly IConfiguration _configuration;
    private readonly JwtService _jwtService;

    public FoodImagePredictionTests()
    {
        _mockDbContext = new Mock<MongoDbContext>();
        _mockFoodCollection = new Mock<IMongoCollection<Food>>();
        _mockLogCollection = new Mock<IMongoCollection<PredictionLog>>();

        var configValues = new Dictionary<string, string?>
        {
            ["AiImageService:BaseUrl"] = "http://127.0.0.1:8001",
            ["AiImageService:TimeoutSeconds"] = "5",
            ["AiImageService:UncertainThreshold"] = "0.5",
            ["Jwt:Secret"] = "test-secret-key-that-is-at-least-32-characters-long"
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        _jwtService = new JwtService(_configuration);

        // Setup mock cursor for Foods collection
        var foodList = new List<Food>
        {
            new() { Id = "651a1b2c3d4e5f6a7b8c9d01", Name = "Phở Bò Truyền Thống", NameEn = "Traditional Beef Pho" },
            new() { Id = "651a1b2c3d4e5f6a7b8c9d02", Name = "Cơm Chiên Dưa Bò", NameEn = "Beef Fried Rice" },
            new() { Id = "651a1b2c3d4e5f6a7b8c9d03", Name = "Nem rán / Chả giò", NameEn = "Spring Rolls" }
        };

        var mockCursor = new Mock<IAsyncCursor<Food>>();
        mockCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(true)
            .Returns(false);
        mockCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockCursor.Setup(c => c.Current).Returns(foodList);

        _mockFoodCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Food>>(),
                It.IsAny<FindOptions<Food, Food>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        _mockDbContext.Setup(db => db.Foods).Returns(_mockFoodCollection.Object);
        _mockDbContext.Setup(db => db.PredictionLogs).Returns(_mockLogCollection.Object);
    }

    private static IFormFile CreateTestFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "image", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task PredictImage_ValidImage_ReturnsOkWith3Elements()
    {
        // 1. Arrange: Fake HttpMessageHandler trả về 3 nhãn giả lập từ Python service
        var fakePythonResponse = new
        {
            top3 = new[]
            {
                new { label = "Phở Bò Truyền Thống", confidence = 0.88 },
                new { label = "Cơm Chiên", confidence = 0.08 },
                new { label = "Nem rán / Chả giò", confidence = 0.04 }
            },
            model_version = "food101-mobilenetv2-v1.0"
        };

        var fakeHandler = new FakeHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(fakePythonResponse), Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(fakeHandler);
        var mockLogger = new Mock<ILogger<FoodImagePredictionService>>();
        var predictionService = new FoodImagePredictionService(httpClient, _configuration, mockLogger.Object);

        var controller = new FoodsController(
            _mockDbContext.Object,
            _jwtService,
            null!, // GeminiService không dùng trong endpoint này
            null!, // IWebHostEnvironment
            predictionService,
            _configuration
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var sampleFile = CreateTestFormFile("pho_bo.jpg", "image/jpeg", new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        // 2. Act
        var result = await controller.PredictImage(sampleFile);

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Lấy property top3 và uncertain bằng reflection
        var top3Prop = okResult.Value.GetType().GetProperty("top3")?.GetValue(okResult.Value) as System.Collections.IEnumerable;
        Assert.NotNull(top3Prop);

        var top3List = top3Prop.Cast<object>().ToList();
        Assert.Equal(3, top3List.Count);

        var uncertainProp = okResult.Value.GetType().GetProperty("uncertain")?.GetValue(okResult.Value);
        Assert.Equal(false, uncertainProp);
    }

    [Fact]
    public async Task PredictImage_MissingFile_Returns400BadRequest()
    {
        // Arrange
        var mockPredictionService = new Mock<IFoodImagePredictionService>();
        var controller = new FoodsController(
            _mockDbContext.Object,
            _jwtService,
            null!,
            null!,
            mockPredictionService.Object,
            _configuration
        );

        // Act: Gửi null file
        var result = await controller.PredictImage(null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task PredictImage_NonImageFile_Returns400BadRequest()
    {
        // Arrange
        var mockPredictionService = new Mock<IFoodImagePredictionService>();
        var controller = new FoodsController(
            _mockDbContext.Object,
            _jwtService,
            null!,
            null!,
            mockPredictionService.Object,
            _configuration
        );

        // File text .txt với ContentType text/plain
        var textFile = CreateTestFormFile("menu.txt", "text/plain", Encoding.UTF8.GetBytes("Danh sách món ăn"));

        // Act
        var result = await controller.PredictImage(textFile);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task PredictImage_AiServiceFails_Returns503ServiceUnavailable()
    {
        // Arrange: Fake HttpMessageHandler ném ngoại lệ HttpRequestException (AI service không chạy)
        var fakeHandler = new FakeHttpMessageHandler(req =>
        {
            throw new HttpRequestException("Không thể kết nối đến máy chủ AI tại http://127.0.0.1:8001");
        });

        var httpClient = new HttpClient(fakeHandler);
        var mockLogger = new Mock<ILogger<FoodImagePredictionService>>();
        var predictionService = new FoodImagePredictionService(httpClient, _configuration, mockLogger.Object);

        var controller = new FoodsController(
            _mockDbContext.Object,
            _jwtService,
            null!,
            null!,
            predictionService,
            _configuration
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var sampleFile = CreateTestFormFile("food.png", "image/png", new byte[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await controller.PredictImage(sampleFile);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }
}
