using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Controllers
{
    public class ApprovalsController : Controller
    {
        private readonly AppDbContext _context;

        public ApprovalsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Approvals()
        {
            var model = new ApprovalsPageViewModel
            {
                PendingLeaveRequests = await _context.LeaveRequests
                    .Where(request => request.status == "Pending")
                    .OrderByDescending(request => request.LR_id)
                    .ToListAsync(),
                PendingDocuments = await _context.Documents
                    .Where(document => document.status == "Pending")
                    .OrderByDescending(document => document.document_id)
                    .ToListAsync()
            };

            return View("Approvals", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLeaveStatus(int id, string status, string? notes)
        {
            var leave = await _context.LeaveRequests.FindAsync(id);
            if (leave == null)
            {
                return NotFound();
            }

            leave.status = status;
            leave.review_notes = notes;
            leave.reviewed_by = User.Identity?.Name;
            leave.reviewed_date = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Approvals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDocumentStatus(int id, string status, string? notes)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            document.status = status;
            document.review_notes = notes;
            document.reviewed_by = User.Identity?.Name;
            document.reviewed_date = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Approvals));
        }
    }
}
