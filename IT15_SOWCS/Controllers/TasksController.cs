using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    public class TasksController : Controller
    {
        public IActionResult Tasks()
        {
            return View();
        }
    }
}