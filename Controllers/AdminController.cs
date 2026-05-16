using Microsoft.AspNetCore.Mvc;

namespace FoodWiseAi.Api.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult AddFood() => View();
    }
}
