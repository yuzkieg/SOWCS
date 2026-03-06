using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Controllers
{
    public class ApprovalsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public ApprovalsController(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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

            await _notificationService.AddForUserAsync(
                leave.employee_email,
                status == "Approved" ? "Leave Request Approved" : "Leave Request Rejected",
                $"Your {leave.leave_type} request was {status.ToLowerInvariant()}.{(string.IsNullOrWhiteSpace(notes) ? string.Empty : $" Feedback: {notes}")}",
                "Leave",
                "/LeaveRequest/LeaveRequest");
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = status == "Approved"
                ? "Leave request approved."
                : "Leave request rejected.";

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

            await _notificationService.AddForUserAsync(
                document.uploaded_by_email,
                status == "Approved" ? "Document Approved" : "Document Rejected",
                $"Your document \"{document.title}\" was {status.ToLowerInvariant()}.{(string.IsNullOrWhiteSpace(notes) ? string.Empty : $" Feedback: {notes}")}",
                "Document",
                "/Documents/Documents");
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = status == "Approved"
                ? "Document approved."
                : "Document rejected.";

            return RedirectToAction(nameof(Approvals));
        }
    }
}
