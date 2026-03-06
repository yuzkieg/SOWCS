using IT15_SOWCS.Data;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Controllers
{
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Reports(string? tab, DateTime? from, DateTime? to)
        {
            var model = await BuildModelAsync(tab, from, to, previewOnly: true);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(string? tab, DateTime? from, DateTime? to)
        {
            var model = await BuildModelAsync(tab, from, to, previewOnly: false);
            ViewData["GeneratedBy"] = User.Identity?.Name ?? "System";
            ViewData["GeneratedAt"] = DateTime.Now;
            return View(model);
        }

        private async Task<ReportsPageViewModel> BuildModelAsync(string? tab, DateTime? from, DateTime? to, bool previewOnly)
        {
            var activeTab = NormalizeTab(tab);
            var takeCount = previewOnly ? 8 : 200;
            if (from.HasValue && to.HasValue && to.Value.Date < from.Value.Date)
            {
                to = from.Value.Date;
            }

            var startDate = from?.Date;
            var endDate = to?.Date;

            var projectsQuery = _context.Projects.AsQueryable();
            if (startDate.HasValue && endDate.HasValue)
            {
                var rangeStart = startDate.Value;
                var rangeEnd = endDate.Value;
                projectsQuery = projectsQuery.Where(project =>
                    project.start_date.Date <= rangeEnd &&
                    project.due_date.Date >= rangeStart);
            }
            else if (startDate.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.due_date.Date >= startDate.Value);
            }
            else if (endDate.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.start_date.Date <= endDate.Value);
            }
            var projects = await projectsQuery
                .OrderByDescending(project => project.project_id)
                .ToListAsync();

            var tasksQuery = _context.Tasks
                .Include(task => task.Project)
                .AsQueryable();
            if (startDate.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.due_date.Date >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.due_date.Date <= endDate.Value);
            }
            var tasks = await tasksQuery
                .OrderByDescending(task => task.task_id)
                .ToListAsync();

            var employeesQuery = _context.Employees.AsQueryable();
            if (startDate.HasValue)
            {
                employeesQuery = employeesQuery.Where(employee => employee.hire_date.Date >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                employeesQuery = employeesQuery.Where(employee => employee.hire_date.Date <= endDate.Value);
            }
            var employees = await employeesQuery
                .OrderBy(employee => employee.full_name)
                .ToListAsync();

            var leavesQuery = _context.LeaveRequests.AsQueryable();
            if (startDate.HasValue)
            {
                leavesQuery = leavesQuery.Where(leave => leave.start_date.Date >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                leavesQuery = leavesQuery.Where(leave => leave.start_date.Date <= endDate.Value);
            }
            var leaves = await leavesQuery
                .OrderByDescending(leave => leave.LR_id)
                .ToListAsync();

            var model = new ReportsPageViewModel
            {
                ActiveTab = activeTab,
                StartDate = startDate,
                EndDate = endDate,

                TotalProjects = projects.Count,
                ProjectsInProgress = projects.Count(project => string.Equals(project.status, "Active", StringComparison.OrdinalIgnoreCase)),
                ProjectsCompleted = projects.Count(project => string.Equals(project.status, "Completed", StringComparison.OrdinalIgnoreCase)),
                ProjectsOnHold = projects.Count(project => string.Equals(project.status, "On Hold", StringComparison.OrdinalIgnoreCase)),

                TotalTasks = tasks.Count,
                TasksCompleted = tasks.Count(task => string.Equals(task.status, "Completed", StringComparison.OrdinalIgnoreCase)),
                TasksInProgress = tasks.Count(task => string.Equals(task.status, "In Progress", StringComparison.OrdinalIgnoreCase)),
                TasksPendingReview = tasks.Count(task => string.Equals(task.status, "Review", StringComparison.OrdinalIgnoreCase)),

                TotalEmployees = employees.Count,
                ActiveEmployees = employees.Count(employee => employee.is_active),
                TotalDepartments = employees.Select(employee => employee.department).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                TotalManagers = employees.Count(employee => string.Equals(employee.employee_role, "Manager", StringComparison.OrdinalIgnoreCase)),

                TotalLeaveRequests = leaves.Count,
                PendingLeaveRequests = leaves.Count(leave => string.Equals(leave.status, "Pending", StringComparison.OrdinalIgnoreCase)),
                ApprovedLeaveRequests = leaves.Count(leave => string.Equals(leave.status, "Approved", StringComparison.OrdinalIgnoreCase)),
                RejectedLeaveRequests = leaves.Count(leave => string.Equals(leave.status, "Rejected", StringComparison.OrdinalIgnoreCase))
            };

            model.RecordsMatch = activeTab switch
            {
                "tasks" => tasks.Count,
                "employees" => employees.Count,
                "leave" => leaves.Count,
                _ => projects.Count
            };

            model.ProjectRows = projects
                .Take(takeCount)
                .Select(project => new ProjectReportRow
                {
                    Name = project.name,
                    Status = project.status,
                    Priority = project.priority,
                    Progress = project.progress
                })
                .ToList();

            model.TaskRows = tasks
                .Take(takeCount)
                .Select(task => new TaskReportRow
                {
                    Title = task.title,
                    Project = task.Project?.name ?? task.project_name ?? "-",
                    Status = task.status,
                    Priority = task.priority
                })
                .ToList();

            model.EmployeeRows = employees
                .Take(takeCount)
                .Select(employee => new EmployeeReportRow
                {
                    Name = employee.full_name,
                    Department = string.IsNullOrWhiteSpace(employee.department) ? "-" : employee.department,
                    Position = string.IsNullOrWhiteSpace(employee.position) ? "-" : employee.position,
                    Role = string.IsNullOrWhiteSpace(employee.employee_role) ? "-" : employee.employee_role.ToLowerInvariant()
                })
                .ToList();

            model.LeaveRows = leaves
                .Take(takeCount)
                .Select(leave => new LeaveReportRow
                {
                    Employee = leave.employee_name,
                    Type = leave.leave_type,
                    StartDate = leave.start_date,
                    EndDate = leave.end_date,
                    Days = leave.days_count,
                    Status = leave.status.ToLowerInvariant(),
                    ReviewedBy = string.IsNullOrWhiteSpace(leave.reviewed_by) ? "-" : leave.reviewed_by
                })
                .ToList();

            return model;
        }

        private static string NormalizeTab(string? tab)
        {
            return (tab ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "tasks" => "tasks",
                "employees" => "employees",
                "leave" => "leave",
                _ => "projects"
            };
        }
    }
}
