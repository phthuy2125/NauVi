using System;

namespace FoodWiseAi.Api.Security
{
    /// <summary>
    /// Biện pháp bảo mật 3: Che bớt thông tin định danh cá nhân (PII) trong log nhật ký máy chủ.
    /// </summary>
    public static class LogMasker
    {
        /// <summary>
        /// Che địa chỉ email để ghi log an toàn.
        /// Quy tắc: phần trước @ dài hơn 2 ký tự thì giữ 2 ký tự đầu + "***"; ngắn hơn hoặc bằng 2 thì "***"; chuỗi rỗng hoặc null trả "".
        /// </summary>
        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return string.Empty;

            int atIndex = email.IndexOf('@');
            if (atIndex < 0)
            {
                return email.Length > 2 ? email[..2] + "***" : "***";
            }

            string localPart = email[..atIndex];
            string domainPart = email[atIndex..];

            string maskedLocal = localPart.Length > 2
                ? localPart[..2] + "***"
                : "***";

            return maskedLocal + domainPart;
        }
    }
}

// Hỗ trợ namespace NauVi.Security theo hợp đồng bài test xUnit
namespace NauVi.Security
{
    public static class LogMasker
    {
        public static string MaskEmail(string? email) =>
            FoodWiseAi.Api.Security.LogMasker.MaskEmail(email);
    }
}
