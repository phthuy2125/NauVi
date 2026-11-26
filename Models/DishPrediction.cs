namespace FoodWiseAi.Api.Models
{
    /// <summary>
    /// Đại diện cho một món ăn được mô hình AI dự đoán với độ tin cậy tương ứng.
    /// </summary>
    public class DishPrediction
    {
        public string Name { get; set; } = string.Empty;
        public double Confidence { get; set; }

        public DishPrediction() { }

        public DishPrediction(string name, double confidence)
        {
            Name = name;
            Confidence = confidence;
        }
    }
}

// Hỗ trợ namespace NauVi.Models theo hợp đồng bài kiểm thử
namespace NauVi.Models
{
    public class DishPrediction : FoodWiseAi.Api.Models.DishPrediction
    {
        public DishPrediction() : base() { }
        public DishPrediction(string name, double confidence) : base(name, confidence) { }
    }
}
