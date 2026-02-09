using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    [Authorize] // This prevents guest access
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            // This will look for Views/Dashboard/Index.cshtml 
            // which should use DashboardLayout.cshtml
            return View();
        }
    }
}