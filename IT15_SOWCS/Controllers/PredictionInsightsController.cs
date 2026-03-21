using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Controllers
{
    [Authorize]
    public class PredictionInsightsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ApprovalPredictionService _predictionService;

        public PredictionInsightsController(
            AppDbContext context,
            UserManager<Users> userManager,
            ApprovalPredictionService predictionService)
        {
            _context = context;
            _userManager = userManager;
            _predictionService = predictionService;
        }

        [HttpGet]
        public async Task<IActionResult> PredictionInsights(string? period, string? tab)
        {
            var user = await _userManager.GetUserAsync(User);
            if (!string.Equals(user?.Role, "superadmin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var model = await BuildInsightsModelAsync(period, tab);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> RecordAction(
            int employeeId,
            string employeeName,
            string predictionLabel,
            string actionType,
            string periodType)
        {
            var user = await _userManager.GetUserAsync(User);
            if (!string.Equals(user?.Role, "superadmin", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var normalizedPeriod = string.Equals(periodType, "year", StringComparison.OrdinalIgnoreCase) ? "year" : "month";
            var periodStart = normalizedPeriod == "year"
                ? new DateTime(DateTime.Today.Year, 1, 1)
                : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var periodEnd = normalizedPeriod == "year"
                ? new DateTime(DateTime.Today.Year, 12, 31)
                : periodStart.AddMonths(1).AddDays(-1);

            var action = new PredictionAction
            {
                employee_id = employeeId,
                employee_name = employeeName ?? string.Empty,
                prediction_label = predictionLabel ?? "Stable",
                action_type = actionType ?? "dismiss",
                created_by = user?.Email ?? User.Identity?.Name,
                created_at = DateTime.UtcNow,
                period_type = normalizedPeriod,
                period_start = periodStart,
                period_end = periodEnd
            };

            _context.PredictionActions.Add(action);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        private async Task<PredictionInsightsViewModel> BuildInsightsModelAsync(string? period, string? tab)
        {
            var periodKey = string.Equals(period, "year", StringComparison.OrdinalIgnoreCase) ? "year" : "month";
            var tabKey = string.IsNullOrWhiteSpace(tab) ? "all" : tab.Trim().ToLowerInvariant();

            var periodStart = periodKey == "year"
                ? new DateTime(DateTime.Today.Year, 1, 1)
                : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var periodEnd = periodKey == "year"
                ? new DateTime(DateTime.Today.Year, 12, 31)
                : periodStart.AddMonths(1).AddDays(-1);

            var users = await _context.Users
                .AsNoTracking()
                .Select(dbUser => new
                {
                    dbUser.Id,
                    dbUser.Email,
                    dbUser.Role,
                    FullName = string.IsNullOrWhiteSpace(dbUser.FullName) ? dbUser.Email : dbUser.FullName
                })
                .ToListAsync();

            var employees = await _context.Employees
                .AsNoTracking()
                .Select(employee => new
                {
                    employee.employee_id,
                    employee.user_id,
                    employee.full_name,
                    employee.department,
                    employee.employee_role
                })
                .ToListAsync();

            var leaves = await _context.LeaveRequests.ToListAsync();
            var documents = await _context.Documents.ToListAsync();

            var rows = new List<PredictionInsightRow>();

            if (_predictionService.IsReady)
            {
                foreach (var userEntry in users.Where(item =>
                             !string.IsNullOrWhiteSpace(item.Email) &&
                             !string.Equals(item.Role, "superadmin", StringComparison.OrdinalIgnoreCase)))
                {
                    var email = userEntry.Email!;
                    var leaveItems = leaves.Where(leave =>
                        string.Equals(leave.employee_email, email, StringComparison.OrdinalIgnoreCase) &&
                        leave.start_date.Date >= periodStart &&
                        leave.start_date.Date <= periodEnd).ToList();

                    var docItems = documents.Where(doc =>
                        string.Equals(doc.uploaded_by_email, email, StringComparison.OrdinalIgnoreCase) &&
                        doc.uploaded_date.Date >= periodStart &&
                        doc.uploaded_date.Date <= periodEnd).ToList();

                    var leaveTotal = leaveItems.Count;
                    var leaveApproved = leaveItems.Count(item => string.Equals(item.status, "Approved", StringComparison.OrdinalIgnoreCase));
                    var leaveRejected = leaveItems.Count(item => string.Equals(item.status, "Rejected", StringComparison.OrdinalIgnoreCase));

                    var docTotal = docItems.Count;
                    var docApproved = docItems.Count(item => string.Equals(item.status, "Approved", StringComparison.OrdinalIgnoreCase));
                    var docRejected = docItems.Count(item => string.Equals(item.status, "Rejected", StringComparison.OrdinalIgnoreCase));

                    var totalRequests = leaveTotal + docTotal;
                    var totalApproved = leaveApproved + docApproved;
                    var totalRejected = leaveRejected + docRejected;

                    var approvalRate = totalRequests == 0 ? 0 : (double)totalApproved / totalRequests;
                    var rejectRate = totalRequests == 0 ? 0 : (double)totalRejected / totalRequests;

                    var features = new ApprovalPredictionFeatures
                    {
                        WindowType = periodKey,
                        LeaveTotal = leaveTotal,
                        LeaveApproved = leaveApproved,
                        LeaveRejected = leaveRejected,
                        DocTotal = docTotal,
                        DocApproved = docApproved,
                        DocRejected = docRejected,
                        TotalRequests = totalRequests,
                        TotalApproved = totalApproved,
                        TotalRejected = totalRejected,
                        OverallApprovalRate = approvalRate,
                        OverallRejectRate = rejectRate
                    };

                    var prediction = _predictionService.Predict(features);
                    var predictionLabel = FormatPredictionLabel(prediction.Label);

                    var profile = employees.FirstOrDefault(item => item.user_id == userEntry.Id);
                    var roleLabel = profile == null
                        ? "Unassigned"
                        : $"{profile.department} - {profile.employee_role}";

                    rows.Add(new PredictionInsightRow
                    {
                        EmployeeId = profile?.employee_id ?? 0,
                        Name = profile?.full_name ?? userEntry.FullName ?? email,
                        RoleLabel = roleLabel,
                        TotalRequests = totalRequests,
                        TotalApproved = totalApproved,
                        TotalRejected = totalRejected,
                        Prediction = predictionLabel,
                        SuggestedAction = SuggestAction(predictionLabel),
                        Confidence = prediction.Confidence
                    });
                }
            }

            if (tabKey != "all")
            {
                rows = rows
                    .Where(row => row.Prediction.Replace(" ", "_").Equals(tabKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new PredictionInsightsViewModel
            {
                Period = periodKey,
                ActiveTab = tabKey,
                Rows = rows.OrderByDescending(row => row.Confidence).ToList()
            };
        }

        private static string FormatPredictionLabel(string label)
        {
            var normalized = (label ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "skill_gap" => "Skill Gap",
                "reward_milestone" => "Reward Milestone",
                _ => "Stable"
            };
        }

        private static string SuggestAction(string prediction)
        {
            return prediction switch
            {
                "Skill Gap" => "Needs training plan and coaching.",
                "Reward Milestone" => "Eligible for recognition or bonus.",
                _ => "Needs improvement to qualify for nomination."
            };
        }
    }
}


