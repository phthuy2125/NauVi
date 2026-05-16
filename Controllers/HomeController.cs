using Microsoft.AspNetCore.Mvc;

namespace FoodWiseAi.Api.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
        public IActionResult ListFood() => View();
        public IActionResult TasteProfile() => View();
        public IActionResult Favorites() => View();
        public IActionResult History() => View();
        public IActionResult IngredSuggestion() => View();
        public IActionResult FoodDetail(string id)
        {
            ViewBag.FoodId = id;
            return View();
        }

        public IActionResult CookMode(string id)
        {
            ViewBag.FoodId = id;
            return View();
        }
    }
}
