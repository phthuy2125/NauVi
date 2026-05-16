namespace FoodWiseAi.Api.Utilities;

public static class LocalTimeStatusHelper
{
    public static string GetCurrentStatus(DateTime? now = null)
    {
        var hour = (now ?? DateTime.Now).Hour;
        if (hour >= 6 && hour <= 8) return "Cao điểm sáng";
        if (hour >= 11 && hour <= 13) return "Cao điểm trưa";
        if (hour >= 17 && hour <= 20) return "Cao điểm tối";
        if (hour >= 22 || hour <= 5) return "Ăn khuya";
        return "Bình thường";
    }
}
