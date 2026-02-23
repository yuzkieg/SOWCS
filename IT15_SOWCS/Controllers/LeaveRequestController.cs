using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    public class LeaveRequestController : Controller
    {
        public IActionResult LeaveRequest()
        {
            return View();
        }
    }
}