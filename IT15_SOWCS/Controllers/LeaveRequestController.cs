using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    public class LeaveRequestController : Controller
    {
        public IActionResult LeaveRequest()
        {
            // You would typically fetch data from a database here
            return View();
        }
    }
}