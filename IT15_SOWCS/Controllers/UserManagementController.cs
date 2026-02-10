using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    public class UserManagementController : Controller
    {
        public IActionResult UserManagement()
        {
            return View("UserManagement");
        }
    }
}