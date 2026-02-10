using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    public class ProjectsController : Controller
    {
        // GET: /Projects/Index
        public IActionResult Projects()
        {
            // Set the title for the Navbar in the layout
            ViewData["Title"] = "Projects";
            return View();
        }
    }
}