using FoodWiseAi.Api.Controllers;
using FoodWiseAi.Api.Models;
using FoodWiseAi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;

namespace FoodWiseAi.Tests;

/*
 * HỢP ĐỒNG mà code phải đáp ứng để các test này chạy được.
 *
 *  1. interface IEmailSender
 *       Task SendAsync(string toEmail, string subject, string htmlBody, string textBody);
 *  2. interface IFoodService  (CHỈNH: nếu project đã có service tra món thì dùng lại tên đó)
 *       Task<Food?> GetByIdAsync(string id);   // không thấy món thì trả null
 *  3. class SendRecipeEmailRequest { string Email }
 *  4. class RecipeEmailController(IFoodService foods, IEmailSender sender)
 *       Task<IActionResult> SendRecipe(string foodId, SendRecipeEmailRequest request)
 *     - email rỗng hoặc sai định dạng  -> 400
 *     - món không tồn tại              -> 404
 *     - gửi thành công                 -> 200
 *     - IEmailSender ném lỗi           -> 503
 */
public class RecipeEmailControllerTests
{
    private const string FoodIdCo = "665f1a2b3c4d5e6f7a8b9c01";   // món có trong dữ liệu mẫu
    private const string FoodIdKhong = "665f1a2b3c4d5e6f7a8b9cff"; // món không tồn tại

    // ---- "Hàng giả": giúp test chạy không cần mạng, Gmail hay MongoDB ----

    private class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject)> DaGui { get; } = new();
        public bool NemLoi { get; set; }

        public Task SendAsync(string toEmail, string subject, string htmlBody, string textBody)
        {
            if (NemLoi) throw new InvalidOperationException("SMTP lỗi giả lập");
            DaGui.Add((toEmail, subject));
            return Task.CompletedTask;
        }
    }

    private class FakeFoodService : IFoodService
    {
        public Task<Food?> GetByIdAsync(string id)
        {
            // CHỈNH: thuộc tính của Food theo model thật
            Food? mon = id == FoodIdCo ? new Food { Id = FoodIdCo, Name = "Phở bò" } : null;
            return Task.FromResult(mon);
        }
    }

    private static (RecipeEmailController controller, FakeEmailSender sender) TaoController(bool smtpLoi = false)
    {
        var sender = new FakeEmailSender { NemLoi = smtpLoi };
        var controller = new RecipeEmailController(new FakeFoodService(), sender);
        return (controller, sender);
    }

    // Lấy mã trạng thái (200, 400, 404, 503...) từ kết quả của controller
    private static int? MaTrangThai(IActionResult ketQua) =>
        Assert.IsAssignableFrom<IStatusCodeActionResult>(ketQua).StatusCode;

    // ---- 5 test do bạn viết trước (KHÔNG sửa đổi) ----

    [Fact]
    public async Task EmailRong_Tra400_VaKhongGuiMail()
    {
        var (controller, sender) = TaoController();

        var ketQua = await controller.SendRecipe(FoodIdCo, new SendRecipeEmailRequest { Email = "" });

        Assert.Equal(400, MaTrangThai(ketQua));
        Assert.Empty(sender.DaGui);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("lan.anh@")]
    [InlineData("@gmail.com")]
    [InlineData("lan anh@gmail.com")]
    public async Task EmailSaiDinhDang_Tra400_VaKhongGuiMail(string emailSai)
    {
        var (controller, sender) = TaoController();

        var ketQua = await controller.SendRecipe(FoodIdCo, new SendRecipeEmailRequest { Email = emailSai });

        Assert.Equal(400, MaTrangThai(ketQua));
        Assert.Empty(sender.DaGui);
    }

    [Fact]
    public async Task MonKhongTonTai_Tra404_VaKhongGuiMail()
    {
        var (controller, sender) = TaoController();

        var ketQua = await controller.SendRecipe(FoodIdKhong, new SendRecipeEmailRequest { Email = "lan.anh@example.com" });

        Assert.Equal(404, MaTrangThai(ketQua));
        Assert.Empty(sender.DaGui);
    }

    [Fact]
    public async Task EmailHopLe_Tra200_VaGuiDungMotLanDenDungDiaChi()
    {
        var (controller, sender) = TaoController();

        var ketQua = await controller.SendRecipe(FoodIdCo, new SendRecipeEmailRequest { Email = "lan.anh@example.com" });

        Assert.Equal(200, MaTrangThai(ketQua));
        var thu = Assert.Single(sender.DaGui);
        Assert.Equal("lan.anh@example.com", thu.To);
        Assert.Contains("Phở bò", thu.Subject);
    }

    [Fact]
    public async Task SmtpLoi_Tra503_KhongSapUngDung()
    {
        var (controller, _) = TaoController(smtpLoi: true);

        var ketQua = await controller.SendRecipe(FoodIdCo, new SendRecipeEmailRequest { Email = "lan.anh@example.com" });

        Assert.Equal(503, MaTrangThai(ketQua));
    }

    // ---- Test bổ sung đề xuất: Kiểm tra giới hạn tần suất (Rate Limit 5 lần / 10 phút -> 429) ----

    [Fact]
    public async Task GuiQua5LanTrong10Phut_Tra429_VaChanGoiSmtp()
    {
        var sender = new FakeEmailSender();
        var rateLimiter = new RecipeEmailRateLimiter(maxRequests: 5, windowMinutes: 10);
        var controller = new RecipeEmailController(new FakeFoodService(), sender, null, rateLimiter);

        // Gửi 5 lần thành công
        for (int i = 0; i < 5; i++)
        {
            var res = await controller.SendRecipe(FoodIdCo, new SendRecipeEmailRequest { Email = "lan.anh@example.com" });
            Assert.Equal(200, MaTrangThai(res));
        }

        // Lần thứ 6 phải bị chặn với mã 429 Too Many Requests
        var lanThu6 = await controller.SendRecipe(FoodIdCo, new SendRecipeEmailRequest { Email = "lan.anh@example.com" });
        Assert.Equal(429, MaTrangThai(lanThu6));

        // Đảm bảo số lượng email gửi thực tế chỉ dừng ở 5, không gửi thêm lần thứ 6
        Assert.Equal(5, sender.DaGui.Count);
    }
}
