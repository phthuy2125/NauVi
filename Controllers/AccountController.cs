using Microsoft.AspNetCore.Mvc;

namespace FoodWiseAi.Api.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Login() => View();
        public IActionResult Register() => View();
        public IActionResult Profile() => View();
    }
}
