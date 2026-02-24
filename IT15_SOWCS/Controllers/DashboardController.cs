using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public DashboardController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;
            var userId = user?.Id ?? string.Empty;

            var employeeIds = await _context.Employees
                .Where(employee => employee.user_id == userId)
                .Select(employee => employee.employee_id)
                .ToListAsync();

            var allTasksQuery = _context.Tasks
                .Include(task => task.Project)
                .AsQueryable();

            var assignedTasksQuery = allTasksQuery
                .Where(task => task.assigned_to == userEmail || employeeIds.Contains(task.employee_id));

            var assignedTaskCount = await assignedTasksQuery.CountAsync();

            // Keep dashboard task counts aligned with what users currently see in Tasks module.
            var effectiveTasksQuery = assignedTaskCount > 0 ? assignedTasksQuery : allTasksQuery;

            var myTasks = await effectiveTasksQuery
                .OrderBy(task => task.due_date)
                .Take(6)
                .ToListAsync();

            var myTaskCount = await effectiveTasksQuery.CountAsync();
            var myCompletedTaskCount = await effectiveTasksQuery.CountAsync(task => task.status == "Completed");
            var overallProgressPercent = myTaskCount == 0
                ? 0
                : (int)Math.Round((myCompletedTaskCount * 100.0) / myTaskCount);

            var recentDocuments = await _context.Documents
                .OrderByDescending(document => document.uploaded_date)
                .Take(6)
                .ToListAsync();

            var recentLeaves = await _context.LeaveRequests
                .OrderByDescending(request => request.reviewed_date ?? request.start_date)
                .Take(6)
                .ToListAsync();

            var activities = new List<DashboardActivityItemViewModel>();

            activities.AddRange(recentDocuments.Select(document => new DashboardActivityItemViewModel
            {
                Type = "Document",
                Title = document.title,
                Subtitle = $"Uploaded by {document.uploaded_by_email ?? "Unknown User"}",
                Status = document.status,
                Date = document.uploaded_date
            }));

            activities.AddRange(recentLeaves.Select(request => new DashboardActivityItemViewModel
            {
                Type = "Leave",
                Title = $"{request.leave_type} Request",
                Subtitle = $"{request.start_date:MMM d} - {request.end_date:MMM d}",
                Status = request.status,
                Date = request.reviewed_date ?? request.start_date
            }));

            var model = new DashboardViewModel
            {
                FullName = string.IsNullOrWhiteSpace(user?.FullName) ? "User" : user!.FullName,
                ActiveProjectsCount = await _context.Projects.CountAsync(project => project.status != "Completed"),
                MyTasksCount = myTaskCount,
                CompletedTasksCount = myCompletedTaskCount,
                DocumentsCount = await _context.Documents.CountAsync(),
                PendingLeavesCount = await _context.LeaveRequests.CountAsync(request => request.status == "Pending"),
                OverallProgressPercent = overallProgressPercent,
                MyTasks = myTasks.Select(task => new DashboardTaskItemViewModel
                {
                    Title = task.title,
                    ProjectName = task.Project?.name ?? task.project_name ?? "Project",
                    Status = task.status,
                    DueDate = task.due_date
                }).ToList(),
                RecentActivities = activities
                    .OrderByDescending(activity => activity.Date)
                    .Take(6)
                    .ToList()
            };

            return View(model);
        }
    }
}
