using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class LeaveRequestController : Controller
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly LeaveBalanceService _leaveBalanceService;

        public LeaveRequestController(
            AppDbContext context,
            NotificationService notificationService,
            LeaveBalanceService leaveBalanceService)
        {
            _context = context;
            _notificationService = notificationService;
            _leaveBalanceService = leaveBalanceService;
        }

        private async Task<bool> IsSuperAdminAsync()
        {
            var currentEmail = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentEmail))
            {
                return false;
            }

            return await _context.Users.AnyAsync(user =>
                user.Email == currentEmail &&
                user.Role != null &&
                user.Role.ToLower() == "superadmin");
        }

        [HttpGet]
        public async Task<IActionResult> LeaveRequest(string? status)
        {
            var currentEmail = User.Identity?.Name;
            Employee? employee = null;
            if (!string.IsNullOrWhiteSpace(currentEmail))
            {
                employee = await _leaveBalanceService.RecomputeBalanceForEmployeeAsync(currentEmail);
            }

            var query = _context.LeaveRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(request => request.status == status);
            }

            var model = new LeaveRequestsPageViewModel
            {
                Requests = await query.OrderByDescending(request => request.LR_id).ToListAsync(),
                Status = status,
                AnnualLeaveBalance = employee?.annual_leave_balance ?? 0,
                SickLeaveBalance = employee?.sick_leave_balance ?? 0,
                PersonalLeaveBalance = employee?.personal_leave_balance ?? 0
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string leaveType, DateTime startDate, DateTime endDate, string? reason)
        {
            if (startDate.Date < DateTime.Today || endDate.Date < DateTime.Today)
            {
                TempData["LeaveError"] = "Leave dates cannot be in the past.";
                return RedirectToAction(nameof(LeaveRequest));
            }

            if (endDate < startDate)
            {
                TempData["LeaveError"] = "End date cannot be earlier than start date.";
                return RedirectToAction(nameof(LeaveRequest));
            }

            var employeeEmail = User.Identity?.Name ?? await _context.Users.Select(user => user.Email).FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(employeeEmail))
            {
                TempData["LeaveError"] = "No employee account available.";
                return RedirectToAction(nameof(LeaveRequest));
            }

            var employeeName = await _context.Users
                .Where(user => user.Email == employeeEmail)
                .Select(user => string.IsNullOrWhiteSpace(user.FullName) ? user.Email! : user.FullName)
                .FirstOrDefaultAsync() ?? employeeEmail;

            var leaveBalanceType = LeaveBalanceService.NormalizeLeaveType(leaveType);
            if (leaveBalanceType.HasValue)
            {
                var employee = await _leaveBalanceService.RecomputeBalanceForEmployeeAsync(employeeEmail);
                if (employee != null)
                {
                    var requestedDays = (decimal)((endDate.Date - startDate.Date).Days + 1);
                    var available = LeaveBalanceService.GetAvailableBalance(employee, leaveBalanceType.Value);
                    if (available < requestedDays)
                    {
                        TempData["LeaveError"] = $"Insufficient {leaveType} balance. Available: {available:0} day(s), requested: {requestedDays:0} day(s).";
                        return RedirectToAction(nameof(LeaveRequest));
                    }
                }
            }

            var leave = new LeaveRequest
            {
                employee_email = employeeEmail,
                employee_name = employeeName,
                leave_type = leaveType,
                start_date = startDate,
                end_date = endDate,
                days_count = (endDate.Date - startDate.Date).Days + 1,
                reason = string.IsNullOrWhiteSpace(reason) ? "N/A" : reason.Trim(),
                status = "Pending"
            };

            _context.LeaveRequests.Add(leave);
            await _notificationService.AddForRoleAsync(
                "manager",
                "New Leave Request",
                $"{employeeName} submitted a {leave.leave_type} request for approval.",
                "LeaveApproval",
                "/Approvals/Approvals");
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(LeaveRequest));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int leaveRequestId, DateTime startDate, DateTime endDate, string? reason)
        {
            var leave = await _context.LeaveRequests.FindAsync(leaveRequestId);
            if (leave == null)
            {
                return NotFound();
            }

            if (leave.status != "Pending")
            {
                TempData["LeaveError"] = "Only pending requests can be edited.";
                return RedirectToAction(nameof(LeaveRequest));
            }

            if (startDate.Date < DateTime.Today || endDate.Date < DateTime.Today)
            {
                TempData["LeaveError"] = "Leave dates cannot be in the past.";
                return RedirectToAction(nameof(LeaveRequest));
            }

            if (endDate.Date < startDate.Date)
            {
                TempData["LeaveError"] = "End date cannot be earlier than start date.";
                return RedirectToAction(nameof(LeaveRequest));
            }

            leave.start_date = startDate;
            leave.end_date = endDate;
            leave.days_count = (endDate.Date - startDate.Date).Days + 1;
            leave.reason = string.IsNullOrWhiteSpace(reason) ? leave.reason : reason.Trim();

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(LeaveRequest));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int leaveRequestId)
        {
            if (!await IsSuperAdminAsync())
            {
                return Forbid();
            }

            var leave = await _context.LeaveRequests.FindAsync(leaveRequestId);
            if (leave == null)
            {
                return NotFound();
            }

            var leaveSnapshot = new
            {
                leave.LR_id,
                leave.employee_email,
                leave.employee_name,
                leave.leave_type,
                leave.start_date,
                leave.end_date,
                leave.days_count,
                leave.reason,
                leave.status,
                leave.review_notes,
                leave.reviewed_by,
                leave.reviewed_date
            };

            _context.ArchiveItems.Add(new ArchiveItem
            {
                source_id = leave.LR_id,
                source_type = "LeaveRequest",
                title = $"{leave.leave_type} - {leave.employee_name}",
                type = "LeaveRequest",
                archived_by = User.Identity?.Name ?? "System",
                date_archived = DateTime.UtcNow,
                reason = string.IsNullOrWhiteSpace(leave.review_notes)
                    ? "Archived from Leave Requests module"
                    : $"Archived from Leave Requests module. Feedback: {leave.review_notes}",
                serialized_data = JsonSerializer.Serialize(leaveSnapshot)
            });

            _context.LeaveRequests.Remove(leave);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Leave request archived successfully.";
            return RedirectToAction(nameof(LeaveRequest));
        }
    }
}
