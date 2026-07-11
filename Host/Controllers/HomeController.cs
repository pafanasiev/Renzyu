using Microsoft.AspNetCore.Mvc;

namespace Host.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View(Guid.NewGuid());
        }

        public IActionResult Game(Guid? token)
        {
            return View(token);
        }

        public IActionResult PrivateGame()
        {
            return RedirectToAction(nameof(Game), new { token = Guid.NewGuid() });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
