using Microsoft.AspNetCore.Mvc;

namespace IT15_SOWCS.Controllers
{
    public class AuditLogsController : Controller
    {
        public IActionResult AuditLogs()
        {
            return View("AuditLogs");
        }
    }
}