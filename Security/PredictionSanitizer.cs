using System;
using System.Collections.Generic;
using System.Linq;
using FoodWiseAi.Api.Models;
using FoodWiseAi.Api.Utilities;

namespace FoodWiseAi.Api.Security
{
    /// <summary>
    /// Biện pháp bảo mật 3: Lọc và làm sạch kết quả AI dự đoán, chống Prompt Injection và ngăn chặn nhãn lạ.
    /// </summary>
    public static class PredictionSanitizer
    {
        /// <summary>
        /// Lọc các dự đoán từ mô hình AI:
        /// - Chỉ giữ nhãn có trong danh sách được phép allowedNames (so khớp không phân biệt hoa thường và dấu, xử lý cả đ/Đ).
        /// - Giữ nguyên thứ tự ban đầu, lấy tối đa 3 phần tử.
        /// - Kẹp điểm tin cậy Confidence trong khoảng [0.0, 1.0].
        /// </summary>
        public static IReadOnlyList<DishPrediction> Filter(
            IEnumerable<DishPrediction> input,
            IReadOnlyCollection<string> allowedNames)
        {
            if (input == null || allowedNames == null || allowedNames.Count == 0)
                return Array.Empty<DishPrediction>();

            // Ánh xạ tên chuẩn hóa (không dấu, không hoa/thường, đ->d) sang tên gốc hợp lệ
            var allowedMap = new Dictionary<string, string>();
            var allowedNormalizedList = new List<(string Normalized, string Original)>();

            foreach (var allowed in allowedNames)
            {
                if (string.IsNullOrWhiteSpace(allowed)) continue;
                var norm = VietnameseTextNormalizer.Normalize(allowed);
                if (!allowedMap.ContainsKey(norm))
                {
                    allowedMap[norm] = allowed;
                    allowedNormalizedList.Add((norm, allowed));
                }
            }

            var result = new List<DishPrediction>();

            foreach (var item in input)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                    continue;

                var normItem = VietnameseTextNormalizer.Normalize(item.Name);

                // 1. So khớp chính xác sau chuẩn hóa không dấu
                string? canonicalName = null;
                if (allowedMap.TryGetValue(normItem, out var exact))
                {
                    canonicalName = exact;
                }
                else
                {
                    // 2. So khớp tương đối nếu nhãn AI viết gọn/viết tắt (chỉ chấp nhận nếu từ khóa đủ dài >= 3 ký tự)
                    if (normItem.Length >= 3)
                    {
                        var partial = allowedNormalizedList.FirstOrDefault(a =>
                            a.Normalized.Contains(normItem) || normItem.Contains(a.Normalized));
                        if (partial.Original != null)
                        {
                            canonicalName = partial.Original;
                        }
                    }
                }

                if (canonicalName != null)
                {
                    // Kẹp confidence trong khoảng [0.0, 1.0]
                    double clampedConfidence = Math.Clamp(item.Confidence, 0.0, 1.0);

                    result.Add(new DishPrediction(canonicalName, clampedConfidence));

                    if (result.Count == 3)
                        break;
                }
            }

            return result;
        }
    }
}

// Hỗ trợ namespace NauVi.Security theo hợp đồng bài test xUnit
namespace NauVi.Security
{
    public static class PredictionSanitizer
    {
        public static IReadOnlyList<FoodWiseAi.Api.Models.DishPrediction> Filter(
            IEnumerable<FoodWiseAi.Api.Models.DishPrediction> input,
            IReadOnlyCollection<string> allowedNames) =>
            FoodWiseAi.Api.Security.PredictionSanitizer.Filter(input, allowedNames);
    }
}
