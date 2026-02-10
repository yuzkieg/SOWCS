using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace IT15_SOWCS.Controllers
{
    public class DocumentsController : Controller
    {
        public IActionResult Documents()
        {
            // Mock data matching your image
            var docs = new List<DocumentViewModel>
            {
                new DocumentViewModel { Title = "IT10 Documentation", FileName = "IT10_DOCUMENTATION.pdf", Status = "Approved", Category = "Other", Size = "10.4 MB", Date = "Jan 24, 2026" },
                new DocumentViewModel { Title = "CCE106 Documentation", FileName = "CCE106_DOCUMENTATION.pdf", Status = "Draft", Category = "Policy", Size = "2 MB", Date = "Jan 24, 2026" },
                new DocumentViewModel { Title = "Employee Handbook 2025", FileName = "employee_handbook_2025.pdf", Status = "Approved", Category = "Policy", Size = "2.3 MB", Date = "Jan 21, 2026" }
            };

            return View(docs);
        }
    }

    public class DocumentViewModel
    {
        public string Title { get; set; }
        public string FileName { get; set; }
        public string Status { get; set; } // Approved, Draft
        public string Category { get; set; }
        public string Size { get; set; }
        public string Date { get; set; }
    }
}