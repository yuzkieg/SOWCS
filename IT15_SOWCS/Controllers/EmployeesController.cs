using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    public class EmployeesController : Controller
    {
        public IActionResult Employees()
        {
            return View("Employees");
        }
    }
}