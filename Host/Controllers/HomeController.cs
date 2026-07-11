using Microsoft.AspNetCore.Mvc;
using Host.Models;

namespace Host.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAiModelCatalog aiModelCatalog;

        public HomeController(IAiModelCatalog aiModelCatalog)
        {
            this.aiModelCatalog = aiModelCatalog
                ?? throw new ArgumentNullException(nameof(aiModelCatalog));
        }

        public IActionResult Index()
        {
            return View(Guid.NewGuid());
        }

        public IActionResult Game(Guid? token)
        {
            var models = aiModelCatalog.GetAvailableModels();
            return View(new GamePageViewModel
            {
                Token = token,
                AiModels = models,
                DefaultAiModelId = models.FirstOrDefault(model => !model.IsBuiltIn)?.Id
                    ?? FileAiModelCatalog.MinimaxModelId,
            });
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
