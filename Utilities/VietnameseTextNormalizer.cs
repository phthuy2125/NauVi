using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FoodWiseAi.Api.Models;

namespace FoodWiseAi.Api.Utilities;

public static class VietnameseTextNormalizer
{
    /// <summary>
    /// Loại bỏ dấu tiếng Việt, ký tự đặc biệt, chuyển thành chữ thường để so sánh tên món.
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Chuyển ký tự 'đ', 'Đ' sang 'd'
        var text = input.Replace("đ", "d").Replace("Đ", "d");

        // Tách các dấu thanh khỏi nguyên âm bằng FormD
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        // Đưa về FormC và loại bỏ dấu câu, chuyển chữ thường
        var clean = stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        clean = Regex.Replace(clean, @"[^a-z0-9\s]", " ");
        clean = Regex.Replace(clean, @"\s+", " ").Trim();
        return clean;
    }

    /// <summary>
    /// Ánh xạ nhãn dự đoán sang món ăn trong cơ sở dữ liệu.
    /// Thứ tự ưu tiên: Khớp tuyệt đối tên gốc/tên tiếng Anh -> Khớp tên đã chuẩn hóa -> Khớp từ khóa chứa nhau.
    /// </summary>
    public static Food? FindMatchingFood(string label, IEnumerable<Food> foods)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var normalizedLabel = Normalize(label);
        var foodList = foods.ToList();

        // 1. So sánh chính xác không phân biệt hoa thường
        var exactMatch = foodList.FirstOrDefault(f =>
            string.Equals(f.Name, label, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(f.NameEn) && string.Equals(f.NameEn, label, StringComparison.OrdinalIgnoreCase))
        );
        if (exactMatch != null) return exactMatch;

        // 2. So sánh sau khi loại bỏ dấu tiếng Việt
        var normalizedMatch = foodList.FirstOrDefault(f =>
        {
            var normName = Normalize(f.Name);
            var normEn = Normalize(f.NameEn);
            return normName == normalizedLabel || (!string.IsNullOrEmpty(normEn) && normEn == normalizedLabel);
        });
        if (normalizedMatch != null) return normalizedMatch;

        // 3. So sánh tương đối: tên chứa nhãn hoặc nhãn chứa tên món
        var containsMatch = foodList.FirstOrDefault(f =>
        {
            var normName = Normalize(f.Name);
            if (string.IsNullOrEmpty(normName) || string.IsNullOrEmpty(normalizedLabel)) return false;
            return normName.Contains(normalizedLabel) || normalizedLabel.Contains(normName);
        });

        return containsMatch;
    }
}
