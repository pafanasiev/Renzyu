using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Host.Models;
using SignalR;

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
