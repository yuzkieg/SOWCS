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
            if (string.Equals(user?.Role, "superadmin", StringComparison.OrdinalIgnoreCase))
            {
                var superAdminModel = await BuildSuperAdminModelAsync(user);
                return View("SuperAdmin", superAdminModel);
            }

            var isProjectManager = await _context.Employees
                .AsNoTracking()
                .AnyAsync(employee =>
                    employee.user_id == user!.Id &&
                    (employee.employee_role.ToLower() == "manager" || employee.employee_role.ToLower() == "project manager"));

            var isHrManager = await _context.Employees
                .AsNoTracking()
                .AnyAsync(employee =>
                    employee.user_id == user!.Id &&
                    (employee.employee_role.ToLower() == "hr manager" || employee.employee_role.ToLower() == "hr"));

            if (isProjectManager)
            {
                var managerModel = await BuildProjectManagerDashboardModelAsync(user);
                return View("ProjectManager", managerModel);
            }

            if (isHrManager)
            {
                var hrModel = await BuildHrManagerDashboardModelAsync(user);
                return View("HrManager", hrModel);
            }

            if (string.Equals(user?.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                var employeeModel = await BuildEmployeeDashboardModelAsync(user);
                return View("Employee", employeeModel);
            }

            var adminModel = await BuildAdminDashboardModelAsync(user);
            return View("Index", adminModel);
        }

        private async Task<DashboardViewModel> BuildEmployeeDashboardModelAsync(Users? user)
        {
            var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;
            var userId = user?.Id ?? string.Empty;

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.user_id == userId);

            var employeeIds = employee == null
                ? await _context.Employees
                    .Where(item => item.user_id == userId)
                    .Select(item => item.employee_id)
                    .ToListAsync()
                : new List<int> { employee.employee_id };

            var assignedTasksQuery = _context.Tasks
                .Include(task => task.Project)
                .Where(task => task.assigned_to == userEmail || employeeIds.Contains(task.employee_id));

            var myTasks = await assignedTasksQuery
                .OrderBy(task => task.due_date)
                .Take(8)
                .ToListAsync();

            var myTaskCount = await assignedTasksQuery.CountAsync();
            var myCompletedTaskCount = await assignedTasksQuery.CountAsync(task => task.status == "Completed");
            var overallProgressPercent = myTaskCount == 0
                ? 0
                : (int)Math.Round((myCompletedTaskCount * 100.0) / myTaskCount);

            var recentDocuments = await _context.Documents
                .OrderByDescending(document => document.uploaded_date)
                .Take(8)
                .ToListAsync();

            var myPendingLeavesCount = await _context.LeaveRequests.CountAsync(request =>
                request.employee_email == userEmail && request.status == "Pending");

            var recentLeaves = await _context.LeaveRequests
                .Where(request => request.employee_email == userEmail)
                .OrderByDescending(request => request.reviewed_date ?? request.start_date)
                .Take(4)
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

            var activeProjectsCount = await assignedTasksQuery
                .Where(task => task.Project != null && task.Project.status != "Completed")
                .Select(task => task.project_id)
                .Distinct()
                .CountAsync();

            return new DashboardViewModel
            {
                FullName = string.IsNullOrWhiteSpace(user?.FullName) ? "Employee" : user!.FullName,
                ActiveProjectsCount = activeProjectsCount,
                MyTasksCount = myTaskCount,
                CompletedTasksCount = myCompletedTaskCount,
                DocumentsCount = await _context.Documents.CountAsync(),
                PendingLeavesCount = myPendingLeavesCount,
                AnnualLeaveBalance = employee?.annual_leave_balance ?? 0,
                SickLeaveBalance = employee?.sick_leave_balance ?? 0,
                PersonalLeaveBalance = employee?.personal_leave_balance ?? 0,
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
                    .Take(8)
                    .ToList()
            };
        }

        private async Task<DashboardViewModel> BuildAdminDashboardModelAsync(Users? user)
        {
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

            return new DashboardViewModel
            {
                FullName = string.IsNullOrWhiteSpace(user?.FullName) ? "User" : user!.FullName,
                ActiveProjectsCount = await _context.Projects.CountAsync(project => project.status != "Completed"),
                MyTasksCount = myTaskCount,
                CompletedTasksCount = myCompletedTaskCount,
                DocumentsCount = await _context.Documents.CountAsync(),
                PendingLeavesCount = await _context.LeaveRequests.CountAsync(request => request.status == "Pending"),
                AnnualLeaveBalance = 0,
                SickLeaveBalance = 0,
                PersonalLeaveBalance = 0,
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
        }

        private async Task<HrManagerDashboardViewModel> BuildHrManagerDashboardModelAsync(Users? user)
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .OrderByDescending(employee => employee.employee_id)
                .ToListAsync();

            var totalEmployees = employees.Count;
            var pendingLeaves = await _context.LeaveRequests
                .AsNoTracking()
                .Where(request => request.status == "Pending")
                .OrderByDescending(request => request.LR_id)
                .Take(8)
                .ToListAsync();

            var departmentLoads = employees
                .GroupBy(employee => string.IsNullOrWhiteSpace(employee.department) ? "Unassigned" : employee.department)
                .Select(group => new HrDepartmentLoadItemViewModel
                {
                    Department = group.Key,
                    Count = group.Count(),
                    Percent = totalEmployees == 0 ? 0 : (int)Math.Round(group.Count() * 100.0 / totalEmployees)
                })
                .OrderByDescending(item => item.Count)
                .Take(6)
                .ToList();

            return new HrManagerDashboardViewModel
            {
                FullName = string.IsNullOrWhiteSpace(user?.FullName) ? "HR Manager" : user!.FullName,
                TotalEmployees = totalEmployees,
                ActiveEmployees = employees.Count(employee => employee.is_active),
                PendingLeaves = await _context.LeaveRequests.CountAsync(request => request.status == "Pending"),
                ApprovedLeaves = await _context.LeaveRequests.CountAsync(request => request.status == "Approved"),
                PendingLeaveRequests = pendingLeaves.Select(request => new HrPendingLeaveItemViewModel
                {
                    EmployeeName = request.employee_name,
                    LeaveType = request.leave_type,
                    Days = request.days_count,
                    StartDate = request.start_date,
                    EndDate = request.end_date,
                    Status = request.status
                }).ToList(),
                DepartmentLoads = departmentLoads,
                RecentEmployees = employees
                    .Take(6)
                    .Select(employee => new HrRecentEmployeeItemViewModel
                    {
                        Initials = GetInitials(employee.full_name),
                        Name = employee.full_name,
                        Role = employee.employee_role
                    })
                    .ToList()
            };
        }

        private async Task<ProjectManagerDashboardViewModel> BuildProjectManagerDashboardModelAsync(Users? user)
        {
            var userEmail = user?.Email ?? User.Identity?.Name ?? string.Empty;

            var managedProjects = await _context.Projects
                .AsNoTracking()
                .Where(project => project.manager_email == userEmail)
                .OrderByDescending(project => project.project_id)
                .ToListAsync();

            var managedProjectIds = managedProjects.Select(project => project.project_id).ToList();

            var tasks = managedProjectIds.Count == 0
                ? new List<WorkTask>()
                : await _context.Tasks
                    .AsNoTracking()
                    .Where(task => managedProjectIds.Contains(task.project_id))
                    .ToListAsync();

            var teamMemberNames = managedProjects
                .SelectMany(project => (project.team_members ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => name.Trim()))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var teamMembers = teamMemberNames.Count == 0
                ? new List<Employee>()
                : await _context.Employees
                    .AsNoTracking()
                    .Where(employee => teamMemberNames.Contains(employee.full_name))
                    .OrderBy(employee => employee.full_name)
                    .Take(8)
                    .ToListAsync();

            var pendingLeaves = await _context.LeaveRequests
                .AsNoTracking()
                .Where(request => request.status == "Pending")
                .OrderByDescending(request => request.LR_id)
                .Take(5)
                .ToListAsync();

            var pendingDocumentsCount = await _context.Documents.CountAsync(document => document.status == "Pending");

            var taskGroupByProject = tasks
                .GroupBy(task => task.project_id)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var total = group.Count();
                        var completed = group.Count(task => string.Equals(task.status, "Completed", StringComparison.OrdinalIgnoreCase));
                        var progress = total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total);
                        return progress;
                    });

            return new ProjectManagerDashboardViewModel
            {
                FullName = string.IsNullOrWhiteSpace(user?.FullName) ? "Project Manager" : user!.FullName,
                ProjectsCount = managedProjects.Count,
                TeamMembersCount = teamMemberNames.Count,
                PendingLeavesCount = pendingLeaves.Count,
                TotalTasks = tasks.Count,
                InProgressTasks = tasks.Count(task => string.Equals(task.status, "In Progress", StringComparison.OrdinalIgnoreCase)),
                OverdueTasks = tasks.Count(task =>
                    task.due_date.Date < DateTime.Today &&
                    !string.Equals(task.status, "Completed", StringComparison.OrdinalIgnoreCase)),
                ApprovalsPendingCount = pendingLeaves.Count + pendingDocumentsCount,
                ProjectProgress = managedProjects
                    .Take(6)
                    .Select(project => new ProjectManagerProjectProgressItemViewModel
                    {
                        Name = project.name,
                        ProgressPercent = taskGroupByProject.TryGetValue(project.project_id, out var progress) ? progress : 0,
                        Status = project.status
                    })
                    .ToList(),
                TaskStatuses = tasks
                    .GroupBy(task => string.IsNullOrWhiteSpace(task.status) ? "Unknown" : task.status)
                    .Select(group => new ProjectManagerTaskStatusItemViewModel
                    {
                        Status = group.Key,
                        Count = group.Count()
                    })
                    .OrderByDescending(item => item.Count)
                    .ToList(),
                TeamMembers = teamMembers
                    .Select(member => new ProjectManagerTeamMemberItemViewModel
                    {
                        Initials = GetInitials(member.full_name),
                        Name = member.full_name,
                        Role = member.employee_role
                    })
                    .ToList(),
                PendingLeaves = pendingLeaves
                    .Select(request => new ProjectManagerPendingLeaveItemViewModel
                    {
                        EmployeeName = request.employee_name,
                        LeaveType = request.leave_type,
                        Days = request.days_count,
                        StartDate = request.start_date,
                        EndDate = request.end_date,
                        Status = request.status
                    })
                    .ToList(),
                MyProjects = managedProjects
                    .Take(8)
                    .Select(project => new ProjectManagerProjectItemViewModel
                    {
                        Name = project.name,
                        Status = project.status,
                        Priority = project.priority
                    })
                    .ToList()
            };
        }

        private async Task<SuperAdminDashboardViewModel> BuildSuperAdminModelAsync(Users? user)
        {
            var employees = await _context.Employees.OrderBy(employee => employee.full_name).ToListAsync();
            var tasks = await _context.Tasks.ToListAsync();
            var leaves = await _context.LeaveRequests.ToListAsync();

            var completedTasks = tasks.Count(task => string.Equals(task.status, "Completed", StringComparison.OrdinalIgnoreCase));
            var totalTasks = tasks.Count;
            var completionRate = totalTasks == 0 ? 0 : (int)Math.Round((completedTasks * 100.0) / totalTasks);
            var inProgressTasks = tasks.Count(task => string.Equals(task.status, "In Progress", StringComparison.OrdinalIgnoreCase));
            var completedThisMonth = tasks.Count(task =>
                task.completed_date.HasValue &&
                task.completed_date.Value.Month == DateTime.UtcNow.Month &&
                task.completed_date.Value.Year == DateTime.UtcNow.Year);
            var completedLastMonth = tasks.Count(task =>
                task.completed_date.HasValue &&
                task.completed_date.Value.Month == DateTime.UtcNow.AddMonths(-1).Month &&
                task.completed_date.Value.Year == DateTime.UtcNow.AddMonths(-1).Year);

            var throughputDelta = completedLastMonth == 0
                ? (completedThisMonth == 0 ? 0 : 100)
                : (int)Math.Round(((completedThisMonth - completedLastMonth) * 100.0) / completedLastMonth);

            var analyticsRows = new List<SuperAdminEmployeeAnalyticsRow>();
            foreach (var employee in employees)
            {
                var employeeTasks = tasks.Where(task => task.employee_id == employee.employee_id).ToList();
                var taskCount = employeeTasks.Count;
                var employeeCompleted = employeeTasks.Count(task => string.Equals(task.status, "Completed", StringComparison.OrdinalIgnoreCase));
                var employeeInReview = employeeTasks.Count(task => string.Equals(task.status, "Review", StringComparison.OrdinalIgnoreCase));
                var employeeCompletedRate = taskCount == 0 ? 0 : (int)Math.Round((employeeCompleted * 100.0) / taskCount);

                var employeeLeaves = leaves.Where(leave => string.Equals(leave.employee_name, employee.full_name, StringComparison.OrdinalIgnoreCase)).ToList();
                var rejectedLeaves = employeeLeaves.Count(leave => string.Equals(leave.status, "Rejected", StringComparison.OrdinalIgnoreCase));
                var rejectRate = employeeLeaves.Count == 0 ? 0 : (int)Math.Round((rejectedLeaves * 100.0) / employeeLeaves.Count);

                var seed = Seed(employee.full_name);
                var wow = (seed % 15) - 3;
                var collaboration = 45 + (seed % 55);

                analyticsRows.Add(new SuperAdminEmployeeAnalyticsRow
                {
                    Initials = GetInitials(employee.full_name),
                    Name = employee.full_name,
                    RoleLabel = $"{employee.department} - {employee.employee_role}",
                    Tasks = taskCount,
                    CompletionPercent = employeeCompletedRate,
                    RejectPercent = rejectRate,
                    WoWPercent = wow,
                    CollaborationScore = collaboration,
                    Classification = Classify(rejectRate, employeeCompletedRate, collaboration, employeeInReview)
                });
            }

            var riskRows = analyticsRows
                .OrderByDescending(row => row.RejectPercent)
                .ThenBy(row => row.CompletionPercent)
                .Take(5)
                .Select(row => new SuperAdminEmployeeModel
                {
                    Initials = row.Initials,
                    Name = row.Name,
                    RoleLabel = row.RoleLabel,
                    RejectRatePercent = row.RejectPercent,
                    CompletionPercent = row.CompletionPercent,
                    WoWPercent = row.WoWPercent,
                    CollaborationScore = row.CollaborationScore,
                    Velocity = Math.Round(Math.Max(0.5m, row.Tasks / 3.0m), 1),
                    Classification = row.Classification
                })
                .ToList();

            var topRows = analyticsRows
                .OrderByDescending(row => row.CompletionPercent)
                .ThenBy(row => row.RejectPercent)
                .Take(3)
                .Select(row => new SuperAdminEmployeeModel
                {
                    Initials = row.Initials,
                    Name = row.Name,
                    RoleLabel = row.RoleLabel,
                    RejectRatePercent = row.RejectPercent,
                    CompletionPercent = row.CompletionPercent,
                    WoWPercent = row.WoWPercent,
                    CollaborationScore = row.CollaborationScore,
                    Velocity = Math.Round(Math.Max(0.5m, row.Tasks / 3.0m), 1),
                    Classification = "Reward Milestone"
                })
                .ToList();

            var suggestions = new List<SuperAdminSuggestionModel>();
            suggestions.AddRange(riskRows.Take(3).Select(row => new SuperAdminSuggestionModel
            {
                Type = "risk",
                Message = $"{row.Name} has a {row.RejectRatePercent}% reject rate this month. Would you like to review the most common rejection reasons?",
                ActionLabel = "Review Rejections"
            }));
            suggestions.AddRange(topRows.Take(3).Select(row => new SuperAdminSuggestionModel
            {
                Type = "positive",
                Message = $"{row.Name} has high monthly KPIs and collaboration score. Nominate for Employee of the Month?",
                ActionLabel = "Nominate"
            }));

            var heatmap = BuildHeatmap(tasks);
            var points = analyticsRows.Select(row => new CorrelationPoint
            {
                Tasks = row.Tasks,
                Reject = row.RejectPercent
            }).ToList();

            var actual = new List<int> { 20, 22, 17 };
            var projected = new List<int> { 20, 22, 17, 20, 23, 25, 26, 28, 30, 32, 34, 36 };

            return new SuperAdminDashboardViewModel
            {
                FullName = string.IsNullOrWhiteSpace(user?.FullName) ? "Super Admin" : user!.FullName,
                TasksCompleted = completedTasks,
                EmployeesCount = employees.Count,
                TaskVelocityDays = Math.Round(tasks.Where(task => task.completed_date.HasValue).Select(task => (task.completed_date!.Value - task.due_date).TotalDays).DefaultIfEmpty(0).Average(), 1),
                Throughput = completedThisMonth,
                ThroughputDeltaPercent = throughputDelta,
                CollaborationScore = analyticsRows.Count == 0 ? 0 : (int)Math.Round(analyticsRows.Average(row => row.CollaborationScore)),
                CompletionRatePercent = completionRate,
                CompletionNumerator = completedTasks,
                CompletionDenominator = totalTasks,
                RejectRisks = riskRows,
                TopPerformers = topRows,
                Suggestions = suggestions,
                AnalyticsRows = analyticsRows.OrderByDescending(row => row.RejectPercent).ThenByDescending(row => row.Tasks).ToList(),
                Heatmap = heatmap,
                CorrelationPoints = points,
                PredictiveActual = actual,
                PredictiveProjected = projected
            };
        }

        private static int Seed(string input)
        {
            unchecked
            {
                var hash = 17;
                foreach (var ch in input)
                {
                    hash = (hash * 31) + ch;
                }
                return Math.Abs(hash);
            }
        }

        private static string GetInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "U";
            }

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
        }

        private static string Classify(int rejectRate, int completionRate, int collaboration, int inReview)
        {
            if (rejectRate >= 18)
            {
                return "Skill Gap Alert";
            }

            if (rejectRate >= 12 || inReview >= 2)
            {
                return "Bottleneck Risk";
            }

            if (completionRate >= 70 && collaboration >= 70)
            {
                return "Reward Milestone";
            }

            return "Stable";
        }

        private static int[,] BuildHeatmap(List<WorkTask> tasks)
        {
            var map = new int[5, 9];
            foreach (var task in tasks)
            {
                var date = task.completed_date ?? task.due_date;
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    continue;
                }

                var row = ((int)date.DayOfWeek) - 1;
                var hour = Math.Clamp(date.Hour, 9, 17);
                var col = hour - 9;
                map[row, col] += 1;
            }

            return map;
        }
    }
}
