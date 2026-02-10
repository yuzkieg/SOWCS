using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    public class ApprovalsController : Controller
    {
        public IActionResult Approvals()
        {
            return View("Approvals");
        }
    }
}