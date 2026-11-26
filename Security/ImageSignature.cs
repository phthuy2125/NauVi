using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FoodWiseAi.Api.Security
{
    /// <summary>
    /// Biện pháp bảo mật 2: Kiểm tra nội dung tệp bằng chữ ký tệp nhị phân (Magic Bytes),
    /// ngăn chặn việc giả mạo ContentType và phần mở rộng của tệp.
    /// </summary>
    public static class ImageSignature
    {
        /// <summary>
        /// Phát hiện định dạng ảnh dựa trên chữ ký nhị phân (magic bytes).
        /// Hỗ trợ: "jpeg", "png", "webp". Trả về null nếu không khớp hoặc tệp không hợp lệ.
        /// </summary>
        public static string? Detect(ReadOnlySpan<byte> header)
        {
            if (header.Length < 3) return null;

            // JPEG: FF D8 FF
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                return "jpeg";
            }

            // PNG: 89 50 4E 47 0D 0A 1A 0A (8 bytes)
            if (header.Length >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            {
                return "png";
            }

            // WebP: RIFF (bytes 0..3: 52 49 46 46) ... WEBP (bytes 8..11: 57 45 42 50) (12 bytes)
            if (header.Length >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            {
                return "webp";
            }

            return null;
        }

        /// <summary>
        /// Đọc an toàn 16 byte đầu của IFormFile mà không làm mất vị trí con trỏ stream.
        /// </summary>
        public static async Task<string?> DetectAsync(IFormFile? file, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0) return null;

            using var stream = file.OpenReadStream();
            byte[] buffer = new byte[16];
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 16), cancellationToken);
            return Detect(buffer.AsSpan(0, bytesRead));
        }
    }
}

// Hỗ trợ namespace NauVi.Security theo hợp đồng bài test xUnit
namespace NauVi.Security
{
    public static class ImageSignature
    {
        public static string? Detect(ReadOnlySpan<byte> header) =>
            FoodWiseAi.Api.Security.ImageSignature.Detect(header);

        public static Task<string?> DetectAsync(IFormFile? file, CancellationToken cancellationToken = default) =>
            FoodWiseAi.Api.Security.ImageSignature.DetectAsync(file, cancellationToken);
    }
}
