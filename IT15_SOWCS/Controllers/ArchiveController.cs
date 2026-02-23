using Microsoft.AspNetCore.Mvc;
using IT15_SOWCS.ViewModels;
using System.Collections.Generic;

namespace IT15_SOWCS.Controllers
{
    public class ArchiveController : Controller
    {
        public IActionResult Archive()
        {
            ViewData["Title"] = "Archive";

            var items = new List<ArchiveItemModel>
            {
                new ArchiveItemModel { Id = 1, Title = "Security Audit Q4 2025", Type = "Task", ArchivedBy = "Security Team", DateArchived = new System.DateTime(2026, 2, 23), Reason = "Audit completed, archived for compliance records" },
                new ArchiveItemModel { Id = 2, Title = "Legacy Website Redesign", Type = "Project", ArchivedBy = "John Manager", DateArchived = new System.DateTime(2026, 2, 23), Reason = "Project completed and no longer needed" },
                new ArchiveItemModel { Id = 3, Title = "Update API Documentation", Type = "Task", ArchivedBy = "Sarah Developer", DateArchived = new System.DateTime(2026, 2, 23), Reason = "Task completed and archived for record keeping" },
                new ArchiveItemModel { Id = 4, Title = "Old HR Policy 2024", Type = "Document", ArchivedBy = "HR Admin", DateArchived = new System.DateTime(2026, 2, 23), Reason = "Replaced by 2025 policy version" }
            };

            return View(items);
        }

        [HttpPost]
        public IActionResult Restore(int id) => RedirectToAction(nameof(Archive));

        [HttpPost]
        public IActionResult Delete(int id) => RedirectToAction(nameof(Archive));
    }
}
