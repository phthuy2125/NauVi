using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;
// CHỈNH: đổi các using dưới đây theo namespace thật của project NauVi
using FoodWiseAi.Api.Controllers;
using FoodWiseAi.Api.Models;
using FoodWiseAi.Api.Services;

namespace FoodWiseAi.Tests;

/*
 * HỢP ĐỒNG mà code phải đáp ứng để các test này chạy được.
 *
 *  1. enum PostStatus { Pending, Approved, Hidden }
 *  2. class Post { string Id; string UserId; string FoodId; string ImageUrl;
 *                  string Content; PostStatus Status; DateTime CreatedAt; }
 *  3. interface IPostRepository
 *       Task AddAsync(Post post);
 *       Task<Post?> GetByIdAsync(string id);
 *       Task<IReadOnlyList<Post>> GetByStatusAsync(PostStatus status);
 *       Task UpdateAsync(Post post);
 *  4. interface IImageStorage            // lưu ảnh, trả về đường dẫn công khai
 *       Task<string> SaveAsync(IFormFile image);
 *  5. interface IFoodService (CHỈNH: dùng lại tên thật nếu project đã có)
 *       Task<Food?> GetByIdAsync(string id);
 *  6. class CreatePostRequest { string? FoodId; string? Content; IFormFile? Image; }
 *  7. class PostsController(IPostRepository posts, IFoodService foods, IImageStorage images)
 *       Task<IActionResult> Create(CreatePostRequest request)  // lấy userId từ ClaimTypes.NameIdentifier
 *       Task<IActionResult> GetPublic()                        // chỉ trả bài Approved
 *       Task<IActionResult> Approve(string id)                 // phải có [Authorize(Roles = "Admin")]
 *     - tạo bài hợp lệ            -> 201, lưu Status = Pending
 *     - thiếu ảnh                 -> 400
 *     - thiếu món (FoodId rỗng)   -> 400
 *     - duyệt bài                 -> 200, Status = Approved
 *     - duyệt bài không tồn tại   -> 404
 */
public class PostsControllerTests
{
    private const string FoodIdCo = "665f1a2b3c4d5e6f7a8b9c01";
    private const string UserId = "user-lan-anh";

    // ---- "Hàng giả": test chạy không cần MongoDB, không ghi file thật ----

    private class FakePostRepository : IPostRepository
    {
        public List<Post> DuLieu { get; } = new();

        public Task AddAsync(Post post) { DuLieu.Add(post); return Task.CompletedTask; }
        public Task<Post?> GetByIdAsync(string id) => Task.FromResult(DuLieu.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Post>> GetByStatusAsync(PostStatus status) =>
            Task.FromResult<IReadOnlyList<Post>>(DuLieu.Where(p => p.Status == status).ToList());
        public Task UpdateAsync(Post post) => Task.CompletedTask; // đối tượng trong bộ nhớ đã được sửa trực tiếp
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

    private class FakeImageStorage : IImageStorage
    {
        public int SoLanLuu { get; private set; }

        public Task<string> SaveAsync(IFormFile image)
        {
            SoLanLuu++;
            return Task.FromResult("/uploads/posts/anh-gia-lap.jpg");
        }
    }

    private static IFormFile AnhGia()
    {
        var stream = new MemoryStream(new byte[10]);
        return new FormFile(stream, 0, 10, "image", "pho-bo.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private static (PostsController controller, FakePostRepository repo, FakeImageStorage storage) TaoController(string? customUserId = null)
    {
        var repo = new FakePostRepository();
        var storage = new FakeImageStorage();
        var controller = new PostsController(repo, new FakeFoodService(), storage);

        // Giả lập người dùng đã đăng nhập
        var id = customUserId ?? UserId;
        var nguoiDung = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, id) }, "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = nguoiDung }
        };
        return (controller, repo, storage);
    }

    private static int? MaTrangThai(IActionResult ketQua) =>
        Assert.IsAssignableFrom<IStatusCodeActionResult>(ketQua).StatusCode;

    // ---- Các test (TC08 -> TC12 trong Bảng 3.1) ----

    [Fact] // TC08
    public async Task DangBaiHopLe_Tra201_VaLuuTrangThaiChoDuyet()
    {
        var (controller, repo, _) = TaoController();
        var yeuCau = new CreatePostRequest { FoodId = FoodIdCo, Content = "Phở bò nhà nấu cuối tuần", Image = AnhGia() };

        var ketQua = await controller.Create(yeuCau);

        Assert.Equal(201, MaTrangThai(ketQua));
        var bai = Assert.Single(repo.DuLieu);
        Assert.Equal(PostStatus.Pending, bai.Status);
        Assert.Equal(UserId, bai.UserId);
        Assert.Equal(FoodIdCo, bai.FoodId);
    }

    [Fact] // TC09a
    public async Task DangBaiThieuAnh_Tra400_VaKhongLuuBai()
    {
        var (controller, repo, _) = TaoController();
        var yeuCau = new CreatePostRequest { FoodId = FoodIdCo, Content = "Chưa có ảnh", Image = null };

        var ketQua = await controller.Create(yeuCau);

        Assert.Equal(400, MaTrangThai(ketQua));
        Assert.Empty(repo.DuLieu);
    }

    [Fact] // TC09b
    public async Task DangBaiThieuMon_Tra400_VaKhongLuuBai()
    {
        var (controller, repo, storage) = TaoController();
        var yeuCau = new CreatePostRequest { FoodId = "", Content = "Chưa gắn món", Image = AnhGia() };

        var ketQua = await controller.Create(yeuCau);

        Assert.Equal(400, MaTrangThai(ketQua));
        Assert.Empty(repo.DuLieu);
        Assert.Equal(0, storage.SoLanLuu); // không lưu ảnh khi dữ liệu chưa hợp lệ
    }

    [Fact] // TC10
    public async Task DuyetBai_Tra200_VaDoiTrangThaiThanhDaDuyet()
    {
        var (controller, repo, _) = TaoController();
        repo.DuLieu.Add(new Post { Id = "bai-1", UserId = UserId, FoodId = FoodIdCo, Status = PostStatus.Pending });

        var ketQua = await controller.Approve("bai-1");

        Assert.Equal(200, MaTrangThai(ketQua));
        Assert.Equal(PostStatus.Approved, repo.DuLieu[0].Status);
    }

    [Fact] // TC10 (mở rộng)
    public async Task DuyetBaiKhongTonTai_Tra404()
    {
        var (controller, _, _) = TaoController();

        var ketQua = await controller.Approve("khong-co");

        Assert.Equal(404, MaTrangThai(ketQua));
    }

    [Fact] // TC11: hành động duyệt bài phải yêu cầu vai trò Admin ở phía máy chủ
    public void DuyetBai_PhaiYeuCauVaiTroAdmin()
    {
        var phuongThuc = typeof(PostsController).GetMethod(nameof(PostsController.Approve));

        var thuocTinh = phuongThuc!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(thuocTinh);
        Assert.Contains("Admin", thuocTinh!.Roles);
    }

    [Fact] // TC12
    public async Task DanhSachCongKhai_ChiGomBaiDaDuyet()
    {
        var (controller, repo, _) = TaoController();
        repo.DuLieu.Add(new Post { Id = "bai-cho", Status = PostStatus.Pending });
        repo.DuLieu.Add(new Post { Id = "bai-duyet", Status = PostStatus.Approved });
        repo.DuLieu.Add(new Post { Id = "bai-an", Status = PostStatus.Hidden });

        var ketQua = await controller.GetPublic();

        var ok = Assert.IsType<OkObjectResult>(ketQua);
        var danhSach = Assert.IsAssignableFrom<IEnumerable<Post>>(ok.Value);
        var bai = Assert.Single(danhSach);
        Assert.Equal("bai-duyet", bai.Id);
    }

    [Fact] // Đề xuất bổ sung: Kiểm tra giới hạn tần suất đăng bài (Rate Limit: tối đa 5 bài / 10 phút)
    public async Task DangBaiVuotQua5LanTrong10Phut_Tra429()
    {
        const string testUser = "user-test-rate-limit";
        var (controller, _, _) = TaoController(testUser);
        var yeuCau = new CreatePostRequest { FoodId = FoodIdCo, Content = "Bài đăng thử nghiệm", Image = AnhGia() };

        // 5 bài đầu tiên hợp lệ -> trả 201
        for (int i = 0; i < 5; i++)
        {
            var res = await controller.Create(yeuCau);
            Assert.Equal(201, MaTrangThai(res));
        }

        // Bài thứ 6 vượt giới hạn -> máy chủ phải chặn và trả 429 Too Many Requests
        var lanThuSau = await controller.Create(yeuCau);
        Assert.Equal(429, MaTrangThai(lanThuSau));
    }
}

