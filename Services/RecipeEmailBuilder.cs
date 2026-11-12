using System.Net;
using System.Text;
using FoodWiseAi.Api.Models;

namespace FoodWiseAi.Api.Services;

/// <summary>
/// Trình tạo nội dung email công thức nấu ăn chuyên nghiệp, chi tiết chuẩn ẩm thực Việt Nam:
/// Đầy đủ định lượng chính xác, tỷ lệ pha gia vị chuẩn vị, thời gian, nhiệt độ và bí quyết đầu bếp.
/// Hỗ trợ cả bản HTML thẩm mỹ cao lẫn bản Plaintext dự phòng.
/// </summary>
public static class RecipeEmailBuilder
{
    public static (string Subject, string HtmlBody, string TextBody) Build(Food food)
    {
        var rawName = string.IsNullOrWhiteSpace(food.Name) ? "Món ngon NauVi" : food.Name.Trim();
        var safeName = WebUtility.HtmlEncode(rawName);
        var safeDesc = WebUtility.HtmlEncode(food.Description ?? "Món ăn truyền thống hấp dẫn, thơm ngon và giàu dưỡng chất cho gia đình.");
        var safeCategory = WebUtility.HtmlEncode(food.Category ?? "Món ngon Việt Nam");
        var safeType = WebUtility.HtmlEncode(food.Type ?? "Món mặn");
        var caloriesText = food.Calories > 0 ? $"{food.Calories} kcal / phần" : "Khoảng 350 - 450 kcal";

        var subject = $"Công thức {rawName} từ NauVi";

        // 1. Phân loại phương pháp nấu chính
        var cookingMethod = DetermineCookingMethod(rawName, food.Type, food.Category);

        // 2. Sinh danh sách nguyên liệu kèm định lượng thực tế
        var detailedIngredients = GenerateIngredientsWithMeasurements(food.Ingredients, rawName, cookingMethod);

        // 3. Sinh tỷ lệ pha nước sốt / gia vị nêm nếm đặc trưng
        var seasoningGuide = GenerateSeasoningGuide(rawName, cookingMethod, food.Taste);

        // 4. Sinh các bước nấu chi tiết theo kỹ thuật ẩm thực
        var steps = GenerateDetailedCookingSteps(rawName, cookingMethod, detailedIngredients);

        // 5. Mẹo bí quyết đầu bếp (Chef's Tips)
        var chefTips = GenerateChefTips(rawName, cookingMethod);

        // 6. Tạo HTML
        var htmlBody = BuildHtmlBody(
            safeName,
            safeCategory,
            safeDesc,
            caloriesText,
            detailedIngredients,
            seasoningGuide,
            steps,
            chefTips);

        // 7. Tạo Plaintext
        var textBody = BuildTextBody(
            rawName,
            food.Category ?? "Món ngon",
            food.Description,
            caloriesText,
            detailedIngredients,
            seasoningGuide,
            steps,
            chefTips);

        return (subject, htmlBody, textBody);
    }

    private enum CookingMethod
    {
        SoupOrNoodle,   // Phở, bún, miến nước
        StirFry,        // Món xào, rang
        BraisedOrSauce, // Món kho, rim, sốt chua ngọt
        DeepFryOrGrill, // Món chiên, rán, nướng
        ClearSoup,      // Canh, lẩu
        FreshSalad,     // Gỏi, nộm, cuốn
        General         // Món thông thường
    }

    private static CookingMethod DetermineCookingMethod(string name, string? type, string? category)
    {
        var lower = (name + " " + (type ?? "") + " " + (category ?? "")).ToLowerInvariant();

        if (lower.Contains("phở") || lower.Contains("bún bò") || lower.Contains("hủ tiếu") || lower.Contains("mì quảng") || (lower.Contains("nước") && lower.Contains("bún")))
            return CookingMethod.SoupOrNoodle;

        if (lower.Contains("sốt") || lower.Contains("chua ngọt") || lower.Contains("kho") || lower.Contains("rim") || lower.Contains("om"))
            return CookingMethod.BraisedOrSauce;

        if (lower.Contains("xào") || lower.Contains("chiên dưa") || lower.Contains("cơm chiên") || lower.Contains("rang") || lower.Contains("áp chảo"))
            return CookingMethod.StirFry;

        if (lower.Contains("canh") || lower.Contains("lẩu"))
            return CookingMethod.ClearSoup;

        if (lower.Contains("chiên") || lower.Contains("rán") || lower.Contains("nướng") || lower.Contains("chả giò") || lower.Contains("nem"))
            return CookingMethod.DeepFryOrGrill;

        if (lower.Contains("gỏi") || lower.Contains("nộm") || lower.Contains("cuốn"))
            return CookingMethod.FreshSalad;

        return CookingMethod.General;
    }

    private static List<string> GenerateIngredientsWithMeasurements(List<string>? rawIngredients, string dishName, CookingMethod method)
    {
        var results = new List<string>();

        if (rawIngredients == null || rawIngredients.Count == 0)
        {
            results.Add("Thành phần chính: 400g - 500g (sơ chế sạch theo công thức)");
            results.Add("Gia vị thông dụng: Nước mắm, đường, hạt nêm, tiêu xay");
            results.Add("Hành tím, tỏi, ớt: 1 củ băm nhuyễn");
            return results;
        }

        foreach (var raw in rawIngredients)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var item = raw.Trim();
            var lower = item.ToLowerInvariant();

            // Nếu đã có định lượng trong tên (ví dụ: "500g thịt gà")
            if (System.Text.RegularExpressions.Regex.IsMatch(lower, @"\d+\s*(g|kg|ml|l|muỗng|thìa|quả|trái|tép|bó|củ)"))
            {
                results.Add(item);
                continue;
            }

            // Gán định lượng phù hợp chuẩn cho gia đình 2 - 3 người
            if (lower.Contains("bò") || lower.Contains("heo") || lower.Contains("lợn") || lower.Contains("gà") || lower.Contains("cánh gà") || lower.Contains("sườn"))
            {
                results.Add($"{item}: 400g – 500g (chặt/thái miếng vừa ăn)");
            }
            else if (lower.Contains("cá") || lower.Contains("tôm") || lower.Contains("mực") || lower.Contains("cua") || lower.Contains("hải sản"))
            {
                results.Add($"{item}: 350g – 400g (làm sạch, để thật ráo nước)");
            }
            else if (lower.Contains("phở") || lower.Contains("bún") || lower.Contains("mì") || lower.Contains("miến"))
            {
                results.Add($"{item}: 400g – 500g (loại tươi hoặc 200g loại khô luộc mềm)");
            }
            else if (lower.Contains("cơm"))
            {
                results.Add($"{item}: 3 – 4 chén cơm nguội (hạt tơi xốp)");
            }
            else if (lower.Contains("trứng"))
            {
                results.Add($"{item}: 2 – 3 quả");
            }
            else if (lower.Contains("đậu phụ") || lower.Contains("đậu hũ"))
            {
                results.Add($"{item}: 3 – 4 bìa (khoảng 300g)");
            }
            else if (lower.Contains("cà chua") || lower.Contains("dưa leo") || lower.Contains("dưa chuột") || lower.Contains("cà rốt") || lower.Contains("ớt chuông"))
            {
                results.Add($"{item}: 2 – 3 quả (thái múi cau hoặc thái lát mỏng)");
            }
            else if (lower.Contains("nấm") || lower.Contains("rau cải") || lower.Contains("xúp lơ") || lower.Contains("giá") || lower.Contains("bí"))
            {
                results.Add($"{item}: 150g – 200g (rửa sạch, cắt khúc vừa ăn)");
            }
            else if (lower.Contains("hành lá") || lower.Contains("ngò") || lower.Contains("rau thơm") || lower.Contains("xà lách"))
            {
                results.Add($"{item}: 1 nắm nhỏ (rửa sạch, thái nhỏ điểm xuyết)");
            }
            else if (lower.Contains("hành tím") || lower.Contains("tỏi") || lower.Contains("sả") || lower.Contains("gừng"))
            {
                results.Add($"{item}: 2 – 3 củ/nhánh (đập dập, băm nhỏ)");
            }
            else
            {
                results.Add($"{item}: 100g – 150g (chuẩn bị sẵn)");
            }
        }

        return results;
    }

    private static List<string> GenerateSeasoningGuide(string dishName, CookingMethod method, List<string>? taste)
    {
        var list = new List<string>();
        var lower = dishName.ToLowerInvariant();

        if (lower.Contains("chua ngọt") || method == CookingMethod.BraisedOrSauce && (taste?.Contains("chua") == true || taste?.Contains("ngọt") == true))
        {
            list.Add("2 muỗng canh nước mắm truyền thống ngon (loại 40 độ đạm)");
            list.Add("2 muỗng canh đường cát trắng (hoặc đường thốt nốt)");
            list.Add("2 muỗng canh giấm gạo tự nhiên (hoặc nước cốt 1 quả chanh tươi)");
            list.Add("1.5 muỗng canh tương cà / tương ớt (tạo màu đỏ bóng và độ sánh)");
            list.Add("1/2 muỗng cà phê hạt tiêu đen xay nhuyễn");
            list.Add("1 muỗng canh tỏi và ớt tươi băm thật nhỏ");
        }
        else if (method == CookingMethod.BraisedOrSauce) // Món kho/rim
        {
            list.Add("3 muỗng canh nước mắm ngon");
            list.Add("1.5 muỗng canh đường nâu (để thắng nước màu cánh gián)");
            list.Add("1 muỗng cà phê hạt nêm, 1/2 muỗng cà phê tiêu xay");
            list.Add("1 bát con nước dừa tươi (hoặc nước lọc ấm)");
            list.Add("2 quả ớt hiểm nguyên trái đập dập nhẹ");
        }
        else if (method == CookingMethod.StirFry) // Món xào / rang
        {
            list.Add("1.5 muỗng canh dầu hào cao cấp (tạo vị đậm đà và bóng bẩy)");
            list.Add("1 muỗng canh nước tương (xì dầu) hoặc nước mắm");
            list.Add("1/2 muỗng cà phê đường, 1/2 muỗng cà phê hạt nêm");
            list.Add("1/2 muỗng cà phê tiêu xay cay nồng");
            list.Add("2 muỗng canh dầu ăn thực vật");
        }
        else if (method == CookingMethod.SoupOrNoodle || method == CookingMethod.ClearSoup) // Nước / Canh
        {
            list.Add("1 muỗng canh muối hạt sạch");
            list.Add("1.5 muỗng canh hạt nêm từ thịt/xương");
            list.Add("1 muỗng canh nước mắm ngon (nêm vào nồi lúc sắp tắt bếp để giữ hương)");
            list.Add("1 mẩu quế, 2 hoa hồi, 1 củ gừng và 2 củ hành tím nướng thơm (nếu là món phở/bún bò)");
            list.Add("Chanh tươi, ớt quả và giấm tỏi ăn kèm");
        }
        else // Món chiên, nướng, thông thường
        {
            list.Add("1 muỗng canh hạt nêm");
            list.Add("1/2 muỗng canh đường");
            list.Add("1 muỗng canh nước mắm");
            list.Add("1/2 muỗng cà phê tiêu xay");
            list.Add("1 muỗng canh tỏi hành băm nhuyễn vắt lấy nước cốt ướp (giúp chiên không bị khét)");
        }

        return list;
    }

    private static List<(string StepName, string Detail, string Duration)> GenerateDetailedCookingSteps(
        string dishName, CookingMethod method, List<string> ingredients)
    {
        var steps = new List<(string, string, string)>();
        var lower = dishName.ToLowerInvariant();

        if (lower.Contains("chua ngọt") || (method == CookingMethod.BraisedOrSauce && lower.Contains("cánh gà")))
        {
            steps.Add((
                "Sơ chế & Khử mùi nguyên liệu",
                "Rửa thịt/cánh gà thật sạch với nước muối loãng pha chút gừng đập dập để khử sạch mùi tanh. Rửa lại nước sạch, khía vài đường nông trên bề mặt thịt để dễ thấm vị rồi dùng khăn giấy thấm THẬT KHÔ ráo (bước quan trọng để khi chiên không bị bắn dầu).",
                "10 phút"
            ));
            steps.Add((
                "Tẩm ướp sơ & Pha nước sốt chua ngọt hoàng kim",
                "Ướp thịt với 1 muỗng cà phê hạt nêm và 1/3 muỗng cà phê tiêu trong 15 phút. Trong lúc chờ, pha chén nước sốt gồm: 2 muỗng canh nước mắm + 2 muỗng canh đường + 2 muỗng canh giấm/chanh + 1.5 muỗng canh tương cà + 2 muỗng canh nước lọc. Khuấy đều cho đường tan hoàn toàn.",
                "15 phút"
            ));
            steps.Add((
                "Chiên giòn vàng đều",
                "Bắc chảo lên bếp, cho lượng dầu vừa đủ ngập mặt đáy. Khi dầu nóng già (nhúng đầu đũa sủi tăm), cho thịt/gà vào chiên ở lửa vừa. Lật đều các mặt đến khi lớp da chuyển màu vàng ruộm giòn tan (khoảng 8-10 phút). Gắp ra đĩa lót giấy thấm dầu.",
                "10 - 12 phút"
            ));
            steps.Add((
                "Đảo sốt chua ngọt óng ả",
                "Chắt bớt dầu trong chảo ra, chỉ để lại 1 muỗng canh. Phi thơm tỏi và ớt băm đến khi dậy mùi vàng thơm, đổ chén nước sốt đã pha vào đun sôi lăn tăn trên lửa nhỏ. Khi sốt bắt đầu sánh nhẹ, trút toàn bộ thịt chiên vào, đảo nhanh và đều tay trong 2-3 phút cho từng miếng thịt áo đều lớp sốt bóng mượt.",
                "5 phút"
            ));
            steps.Add((
                "Trình bày & Thưởng thức",
                "Bày món ăn ra đĩa lớn, rắc thêm chút hạt mè rang thơm (vừng) hoặc vài cọng ngò rí lên trên. Thưởng thức ngay khi còn nóng sốt giòn thơm, rất hợp ăn cùng cơm trắng nóng hổi và dưa leo giòn mát.",
                "2 phút"
            ));
        }
        else if (method == CookingMethod.SoupOrNoodle)
        {
            steps.Add((
                "Sơ chế xương & Khử hôi nước dùng",
                "Rửa sạch xương và thịt bò/gà với nước muối. Chần qua nồi nước sôi bùng khoảng 3-4 phút để thải sạch bọt cặn và mùi tanh, sau đó rửa lại thật sạch dưới vòi nước lạnh.",
                "15 phút"
            ));
            steps.Add((
                "Ninh nước dùng trong vắt & Thơm ngào ngạt",
                "Cho xương vào nồi lớn với 2.5 - 3 lít nước sạch. Nướng xém vỏ gừng, hành tím, quế hồi rồi rửa nhẹ lớp cháy, thả vào nồi cùng 1 thìa muối hạt. Đun sôi rồi hạ lửa thật nhỏ liu riu, hé vung và vớt bọt liên tục để nước dùng đạt độ trong vắt ngọt lịm.",
                "45 - 60 phút"
            ));
            steps.Add((
                "Sơ chế đồ ăn kèm & Nêm nếm",
                "Thái thịt bò/gà thật mỏng ngang thớ. Hành lá chẻ cọng phần đầu trắng, phần lá thái nhỏ cùng ngò rí. Nêm nếm nước dùng với hạt nêm và muối cho vừa khẩu vị đậm đà thanh tao.",
                "10 phút"
            ));
            steps.Add((
                "Trụng bánh & Chan nước dùng",
                "Đun một nồi nước sôi riêng, cho bánh phở/bún vào trụng nhanh 20-30 giây cho nóng mềm rồi cho vào tô. Xếp thịt chín/thịt tái cùng hành hoa lên mặt. Đun nước dùng thật sôi sùng sục rồi múc chan đều lên ngập bát phở.",
                "5 phút"
            ));
            steps.Add((
                "Thưởng thức chuẩn phong vị",
                "Ăn ngay khi còn bốc khói nghi ngút. Ăn kèm quẩy giòn, vắt thêm lát chanh, thêm vài lát ớt tươi và tương ớt truyền thống.",
                "Ngay lập tức"
            ));
        }
        else if (method == CookingMethod.StirFry)
        {
            steps.Add((
                "Sơ chế nguyên liệu & Cắt thái đồng đều",
                "Rửa sạch thịt và rau củ. Thịt thái mỏng ngang thớ để không bị dai. Rau củ cắt khúc hoặc thái lát có độ dày tương đương nhau để chín đều cùng lúc.",
                "10 phút"
            ));
            steps.Add((
                "Ướp thịt đậm đà & Mềm mọng",
                "Ướp thịt với 1 muỗng cà phê dầu hào, 1/2 muỗng cà phê hạt nêm, chút tiêu và đặc biệt là 1 thìa cà phê dầu ăn (dầu ăn giúp khóa ẩm, thịt xào xong mềm ngọt không bị khô xác). Để ngấm 15 phút.",
                "15 phút"
            ));
            steps.Add((
                "Xào thịt trên lửa lớn (Kỹ thuật xào nhanh)",
                "Bật lửa lớn làm chảo thật nóng, cho 1 muỗng dầu ăn phi thơm tỏi băm. Trút thịt vào xào đảo thật nhanh tay trong 2 phút vừa chín tái khoảng 80% thì trút ngay ra đĩa riêng (không xào lâu thịt sẽ bị dai).",
                "3 phút"
            ));
            steps.Add((
                "Xào rau củ & Hòa quyện hương vị",
                "Vẫn trên chiếc chảo đó, thêm chút dầu ăn xào rau củ trên lửa lớn. Nêm chút hạt nêm, xào trong 3-4 phút đến khi rau chín tới giòn ngọt. Trút đĩa thịt và nước ướp chảy ra vào lại chảo, đảo đều tay trên lửa lớn thêm 1 phút cho ngấm quyện.",
                "4 - 5 phút"
            ));
            steps.Add((
                "Hoàn thành & Bày biện",
                "Rắc hạt tiêu đen xay thơm nồng, tắt bếp. Múc ra đĩa sâu lòng, thưởng thức cùng cơm nóng sốt.",
                "2 phút"
            ));
        }
        else // Phương pháp tiêu chuẩn cho các món khác
        {
            steps.Add((
                "Sơ chế sạch sẽ & Chuẩn bị nguyên liệu",
                "Rửa sạch các loại nguyên liệu thịt cá và rau củ quả. Để ráo nước hoàn toàn trước khi chế biến để đảm bảo an toàn vệ sinh và hương vị thơm ngon nhất.",
                "10 phút"
            ));
            steps.Add((
                "Tẩm ướp gia vị chuẩn thời gian",
                "Ướp nguyên liệu với hỗn hợp nước mắm, hạt nêm, tiêu, hành tỏi băm. Trộn đều và để nghỉ tối thiểu 15-20 phút cho gia vị ngấm sâu vào từng thớ thịt.",
                "20 phút"
            ));
            steps.Add((
                "Nấu chín với nhiệt độ chuẩn xác",
                "Điều chỉnh mức lửa vừa vặn. Nấu chín kỹ nguyên liệu theo thứ tự từ phần cứng/chín chậm đến phần mềm/chín nhanh, kiểm tra độ mềm mọng và giữ được màu sắc tự nhiên tươi tắn.",
                "15 - 20 phút"
            ));
            steps.Add((
                "Cân bằng hương vị cuối cùng",
                "Nếm thử lại phần nước sốt hoặc canh, gia giảm một chút mắm ngon hoặc tiêu xay để tạo điểm nhấn đậm đà khó quên.",
                "3 phút"
            ));
            steps.Add((
                "Bày đĩa & Thưởng thức ấm cúng",
                "Trình bày ra đĩa hoặc tô đẹp mắt, điểm thêm vài nhánh rau thơm xanh mướt. Dùng ngay trong bữa cơm gia đình khi còn nóng sốt.",
                "2 phút"
            ));
        }

        return steps;
    }

    private static List<string> GenerateChefTips(string dishName, CookingMethod method)
    {
        var tips = new List<string>();
        var lower = dishName.ToLowerInvariant();

        if (lower.Contains("chua ngọt") || lower.Contains("gà"))
        {
            tips.Add("Để cánh gà/thịt chiên giòn rụm mà không ngấm dầu: sau khi rửa, hãy dùng khăn giấy nhà bếp lau thật khô ráo bề mặt trước khi chiên.");
            tips.Add("Tỷ lệ sốt chua ngọt chuẩn nhà hàng: Tỷ lệ 1 mắm - 1 đường - 1 giấm/chanh luôn tạo ra độ cân bằng hoàn hảo, không bị gắt.");
            tips.Add("Khi đảo sốt: Chỉ cho thịt vào khi sốt đã sôi sánh nhẹ, đảo nhanh trên lửa nhỏ để giữ được độ giòn của lớp vỏ ngoài.");
        }
        else if (method == CookingMethod.SoupOrNoodle || method == CookingMethod.ClearSoup)
        {
            tips.Add("Bí quyết nước dùng trong veo: Tuyệt đối không đậy kín vung khi ninh xương và không bật lửa quá lớn khiến nước bị đục sủi bọt.");
            tips.Add("Hành gừng nướng: Nên cạo sạch lớp than cháy đen bên ngoài trước khi thả vào nồi nước dùng để nước thơm mùi nướng mà không bị xỉn màu.");
        }
        else if (method == CookingMethod.StirFry)
        {
            tips.Add("Kỹ thuật giữ rau xanh mướt: Xào rau trên lửa lớn nhất có thể, đảo liên tục và không đậy nắp chảo.");
            tips.Add("Thịt bò xào không bị dai: Luôn thái ngang thớ và chỉ xào chín tới 80% rồi trút ra, không xào lâu trên chảo.");
        }
        else
        {
            tips.Add("Chọn nguyên liệu: Ưu tiên chọn thịt tươi mới trong ngày, thớ thịt săn chắc có độ đàn hồi tốt.");
            tips.Add("Nước mắm ngon: Luôn nêm nước mắm ở bước cuối cùng trước khi tắt bếp để giữ nguyên vẹn mùi thơm tự nhiên.");
        }

        return tips;
    }

    private static string BuildHtmlBody(
        string safeName,
        string safeCategory,
        string safeDesc,
        string caloriesText,
        List<string> ingredients,
        List<string> seasoningGuide,
        List<(string StepName, string Detail, string Duration)> steps,
        List<string> chefTips)
    {
        var ingredientsHtml = string.Join("", ingredients.Select(i =>
            $"<li style=\"padding: 9px 0; border-bottom: 1px dashed #e2e8f0; color: #334155; font-size: 14px; display: flex; align-items: baseline;\"><span style=\"color: #0284c7; font-weight: bold; margin-right: 10px; font-size: 16px;\">●</span><span>{WebUtility.HtmlEncode(i)}</span></li>"));

        var seasoningHtml = string.Join("", seasoningGuide.Select(s =>
            $"<li style=\"padding: 7px 0; border-bottom: 1px dotted #fde68a; color: #78350f; font-size: 13px;\">★ {WebUtility.HtmlEncode(s)}</li>"));

        var stepsHtml = new StringBuilder();
        for (int i = 0; i < steps.Count; i++)
        {
            var (stepName, detail, duration) = steps[i];
            stepsHtml.Append($"""
            <div style="margin-bottom: 18px; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 16px; box-shadow: 0 1px 3px rgba(0,0,0,0.03);">
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                <div style="display: flex; align-items: center; gap: 8px;">
                  <span style="display: inline-block; width: 26px; height: 26px; background: #0284c7; color: #ffffff; border-radius: 50%; text-align: center; line-height: 26px; font-size: 13px; font-weight: 800;">{i + 1}</span>
                  <strong style="color: #0f172a; font-size: 15px;">{WebUtility.HtmlEncode(stepName)}</strong>
                </div>
                <span style="font-size: 11px; background: #f0f9ff; color: #0284c7; padding: 3px 10px; border-radius: 999px; font-weight: 700; border: 1px solid #bae6fd;">⏱ {WebUtility.HtmlEncode(duration)}</span>
              </div>
              <p style="margin: 0; color: #475569; font-size: 14px; line-height: 1.6; padding-left: 34px;">{WebUtility.HtmlEncode(detail)}</p>
            </div>
            """);
        }

        var chefTipsHtml = string.Join("", chefTips.Select(t =>
            $"<li style=\"margin-bottom: 8px; color: #065f46; font-size: 13px; line-height: 1.5;\">💡 <strong>Mẹo:</strong> {WebUtility.HtmlEncode(t)}</li>"));

        return $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>{{safeName}}</title>
          <style>
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f1f5f9; margin: 0; padding: 16px; }
            .container { max-width: 620px; margin: 0 auto; background: #ffffff; border-radius: 20px; overflow: hidden; border: 1px solid #e2e8f0; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.06); }
            .header { background: linear-gradient(135deg, #0284c7 0%, #0369a1 100%); color: #ffffff; padding: 32px 24px; text-align: center; }
            .badge-row { margin-top: 10px; display: flex; justify-content: center; gap: 8px; flex-wrap: wrap; }
            .pill { background: rgba(255,255,255,0.22); color: #ffffff; padding: 4px 12px; border-radius: 9999px; font-size: 12px; font-weight: 700; }
            .meta-bar { background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 14px 20px; display: flex; justify-content: space-around; text-align: center; }
            .meta-item .label { font-size: 11px; text-transform: uppercase; color: #64748b; font-weight: 700; }
            .meta-item .val { font-size: 14px; font-weight: 800; color: #0f172a; margin-top: 2px; }
            .content { padding: 24px; }
            .section-head { font-size: 16px; font-weight: 800; color: #0369a1; text-transform: uppercase; letter-spacing: 0.5px; margin: 24px 0 12px 0; border-left: 4px solid #0284c7; padding-left: 10px; }
            ul { list-style-type: none; padding: 0; margin: 0; }
            .seasoning-box { background: #fffbeb; border: 1px solid #fef3c7; border-radius: 12px; padding: 16px; margin: 16px 0; }
            .tips-box { background: #ecfdf5; border: 1px solid #a7f3d0; border-radius: 12px; padding: 16px; margin: 20px 0; }
            .footer { background: #f8fafc; padding: 20px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0; line-height: 1.6; }
          </style>
        </head>
        <body>
          <div class="container">
            <div class="header">
              <span style="font-size: 12px; text-transform: uppercase; letter-spacing: 1px; opacity: 0.9; font-weight: 700;">NAUVI CHEF RECIPE</span>
              <h1 style="margin: 6px 0 0 0; font-size: 26px; font-weight: 900; letter-spacing: -0.5px;">{{safeName}}</h1>
              <div class="badge-row">
                <span class="pill">{{safeCategory}}</span>
                <span class="pill">Khẩu phần 2 - 3 người</span>
                <span class="pill">{{caloriesText}}</span>
              </div>
            </div>

            <div class="meta-bar">
              <div class="meta-item">
                <div class="label">Sơ chế</div>
                <div class="val">15 Phút</div>
              </div>
              <div class="meta-item">
                <div class="label">Nấu chính</div>
                <div class="val">20 - 30 Phút</div>
              </div>
              <div class="meta-item">
                <div class="label">Độ khó</div>
                <div class="val">Vừa phải</div>
              </div>
            </div>

            <div class="content">
              <div style="background: #f1f5f9; border-radius: 10px; padding: 14px 18px; color: #334155; font-size: 14px; line-height: 1.5; font-style: italic; margin-bottom: 20px;">
                "{{safeDesc}}"
              </div>

              <div class="section-head">1. Nguyên liệu cần chuẩn bị (Định lượng thực tế)</div>
              <ul style="background: #f8fafc; padding: 12px 18px; border-radius: 12px; border: 1px solid #e2e8f0;">
                {{ingredientsHtml}}
              </ul>

              <div class="seasoning-box">
                <div style="font-size: 14px; font-weight: 800; color: #92400e; margin-bottom: 8px; text-transform: uppercase;">
                  🍯 Tỷ Lệ Vàng Pha Gia Vị / Nước Sốt Chuẩn Vị
                </div>
                <ul>
                  {{seasoningHtml}}
                </ul>
              </div>

              <div class="section-head">2. Hướng dẫn các bước nấu chi tiết</div>
              <div>
                {{stepsHtml.ToString()}}
              </div>

              <div class="tips-box">
                <div style="font-size: 14px; font-weight: 800; color: #065f46; margin-bottom: 8px; text-transform: uppercase;">
                  ✨ Bí Quyết Đầu Bếp Để Món Ăn Hoàn Hảo
                </div>
                <ul>
                  {{chefTipsHtml}}
                </ul>
              </div>
            </div>

            <div class="footer">
              Email công thức nấu ăn này được tạo và gửi tự động từ <strong>NauVi</strong>.<br>
              Bạn không cần tìm kiếm thêm ở nơi khác, hãy chuẩn bị nguyên liệu và vào bếp ngay nhé!<br>
              <em>Chúc bạn và gia đình có một bữa ăn thật ngon miệng và ấm cúng! ❤️</em>
            </div>
          </div>
        </body>
        </html>
        """;
    }

    private static string BuildTextBody(
        string rawName,
        string category,
        string? description,
        string calories,
        List<string> ingredients,
        List<string> seasoningGuide,
        List<(string StepName, string Detail, string Duration)> steps,
        List<string> chefTips)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================");
        sb.AppendLine($"NAUVI CHEF RECIPE - CÔNG THỨC CHI TIẾT: {rawName.ToUpperInvariant()}");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Phân loại: {category} | Khẩu phần: 2-3 người | Năng lượng: {calories}");
        sb.AppendLine($"Thời gian: Sơ chế 15 phút, Nấu 20-30 phút | Độ khó: Vừa phải");
        sb.AppendLine();
        sb.AppendLine("MÔ TẢ MÓN ĂN:");
        sb.AppendLine(description ?? "Món ăn ngon và giàu dinh dưỡng.");
        sb.AppendLine();
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("1. NGUYÊN LIỆU CẦN CHUẨN BỊ (ĐỊNH LƯỢNG THỰC TẾ):");
        sb.AppendLine("----------------------------------------------------------------");
        foreach (var ing in ingredients)
        {
            sb.AppendLine($"  - {ing}");
        }
        sb.AppendLine();
        sb.AppendLine("TỶ LỆ VÀNG PHA GIA VỊ / NƯỚC SỐT:");
        foreach (var s in seasoningGuide)
        {
            sb.AppendLine($"  * {s}");
        }
        sb.AppendLine();
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("2. HƯỚNG DẪN CÁC BƯỚC NẤU CHI TIẾT:");
        sb.AppendLine("----------------------------------------------------------------");
        for (int i = 0; i < steps.Count; i++)
        {
            var (stepName, detail, duration) = steps[i];
            sb.AppendLine($"[BƯỚC {i + 1}] {stepName.ToUpperInvariant()} ({duration})");
            sb.AppendLine($"  {detail}");
            sb.AppendLine();
        }
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("3. BÍ QUYẾT ĐẦU BẾP ĐỂ MÓN ĂN HOÀN HẢO:");
        sb.AppendLine("----------------------------------------------------------------");
        foreach (var tip in chefTips)
        {
            sb.AppendLine($"  * {tip}");
        }
        sb.AppendLine();
        sb.AppendLine("================================================================");
        sb.AppendLine("Email gửi tự động từ NauVi. Chúc bạn nấu ăn thật ngon miệng!");
        sb.AppendLine("================================================================");

        return sb.ToString();
    }
}
