using System;
using System.Web.Mvc;

namespace Host.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var newGameId = Guid.NewGuid();
            return View(newGameId);
        }
        [ActionName("Game")]
        public ActionResult GameAction(Guid? token)
        {
            return View(token);
        }
        public ActionResult PrivateGame()
        {
            return RedirectToAction("Game", new { gameId = Guid.NewGuid() });
        }
    }
}
