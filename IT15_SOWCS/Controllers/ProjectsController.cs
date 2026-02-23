using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    public class ProjectsController : Controller
    {
        public IActionResult Projects()
        {
            ViewData["Title"] = "Projects";
            return View();
        }

    }

}