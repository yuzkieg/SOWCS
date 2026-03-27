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
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ApprovalPredictionService _predictionService;

        public DashboardController(AppDbContext context, UserManager<Users> userManager, ApprovalPredictionService predictionService)
        {
            _context = context;
            _userManager = userManager;
            _predictionService = predictionService;
        }

        private static bool IsProjectManagerRole(string? role)
        {
            var normalized = (role ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "project manager" || normalized == "manager";
        }

        public async Task<IActionResult> Index(string? predictionPeriod)
        {
            var user = await _userManager.GetUserAsync(User);
            if (string.Equals(user?.Role, "superadmin", StringComparison.OrdinalIgnoreCase))
            {
                var superAdminModel = await BuildSuperAdminModelAsync(user, predictionPeriod);
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

        [HttpGet]
        public async Task<IActionResult> SuperAdminPredictions(string? predictionPeriod)
        {
            var user = await _userManager.GetUserAsync(User);
            if (!string.Equals(user?.Role, "superadmin", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var model = await BuildSuperAdminModelAsync(user, predictionPeriod);
            return PartialView("_SuperAdminPredictions", model);
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
            var employeeFullName = user == null
                ? null
                : await _context.Employees
                    .AsNoTracking()
                    .Where(employee => employee.user_id == user.Id)
                    .Select(employee => employee.full_name)
                    .FirstOrDefaultAsync();

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

            var eligibleTeamMembersQuery = teamMemberNames.Count == 0
                ? _context.Employees
                    .AsNoTracking()
                    .Where(employee => false)
                : _context.Employees
                    .AsNoTracking()
                    .Where(employee =>
                        teamMemberNames.Contains(employee.full_name) &&
                        employee.employee_role != null &&
                        (employee.employee_role.ToLower() == "employee" ||
                         employee.employee_role.ToLower() == "project manager" ||
                         employee.employee_role.ToLower() == "manager"));

            var teamMembersCount = await eligibleTeamMembersQuery.CountAsync();
            var teamMembers = await eligibleTeamMembersQuery
                .OrderBy(employee => employee.full_name)
                .Take(8)
                .ToListAsync();

            var pendingDocumentsCount = await _context.Documents.CountAsync(document => document.status == "Pending");
            var pendingDocuments = await _context.Documents
                .AsNoTracking()
                .Where(document => document.status == "Pending")
                .OrderByDescending(document => document.document_id)
                .Take(5)
                .ToListAsync();

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
                FullName = !string.IsNullOrWhiteSpace(employeeFullName)
                    ? employeeFullName
                    : string.IsNullOrWhiteSpace(user?.FullName) ? "Project Manager" : user!.FullName,
                ProjectsCount = managedProjects.Count,
                TeamMembersCount = teamMembersCount,
                PendingDocumentsCount = pendingDocumentsCount,
                TotalTasks = tasks.Count,
                InProgressTasks = tasks.Count(task => string.Equals(task.status, "In Progress", StringComparison.OrdinalIgnoreCase)),
                OverdueTasks = tasks.Count(task =>
                    task.due_date.Date < DateTime.Today &&
                    !string.Equals(task.status, "Completed", StringComparison.OrdinalIgnoreCase)),
                ApprovalsPendingCount = pendingDocumentsCount,
                ProjectProgress = managedProjects
                    .Take(6)
                    .Select(project => new ProjectManagerProjectProgressItemViewModel
                    {
                        Name = project.name,
                        ProgressPercent = taskGroupByProject.TryGetValue(project.project_id, out var progress) ? progress : 0,
                        Status = project.status
                    })
                    .ToList(),
                TaskBreakdownItems = tasks
                    .OrderByDescending(task => string.Equals(task.status, "Completed", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(task => task.due_date)
                    .Take(8)
                    .Select(task => new ProjectManagerTaskBreakdownItemViewModel
                    {
                        TaskTitle = task.title,
                        ProjectName = managedProjects
                            .FirstOrDefault(project => project.project_id == task.project_id)?.name
                            ?? task.project_name
                            ?? "Unknown Project",
                        Status = string.IsNullOrWhiteSpace(task.status) ? "To Do" : task.status
                    })
                    .ToList(),
                TeamMembers = teamMembers
                    .Select(member => new ProjectManagerTeamMemberItemViewModel
                    {
                        Initials = GetInitials(member.full_name),
                        Name = member.full_name,
                        Role = IsProjectManagerRole(member.employee_role) ? "Team Leader" : "Employee"
                    })
                    .ToList(),
                PendingDocuments = pendingDocuments
                    .Select(document => new ProjectManagerPendingDocumentItemViewModel
                    {
                        Title = document.title,
                        UploadedBy = document.uploaded_by_email ?? "Unknown",
                        Category = document.category,
                        UploadedDate = document.uploaded_date,
                        Status = document.status
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

        private async Task<SuperAdminDashboardViewModel> BuildSuperAdminModelAsync(Users? user, string? predictionPeriod)
        {
            var employees = await _context.Employees.OrderBy(employee => employee.full_name).ToListAsync();
            var tasks = await _context.Tasks.ToListAsync();
            var leaves = await _context.LeaveRequests.ToListAsync();
            var documents = await _context.Documents.ToListAsync();

            var employeeUsers = await _context.Users
                .AsNoTracking()
                .Select(dbUser => new { dbUser.Id, dbUser.Role })
                .ToListAsync();

            var employeeUsersById = employeeUsers
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .ToDictionary(item => item.Id, item => item.Role ?? string.Empty);

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
                if (employeeUsersById.TryGetValue(employee.user_id, out var role) &&
                    string.Equals(role, "superadmin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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
                    EmployeeId = employee.employee_id,
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
                    EmployeeId = row.EmployeeId,
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
                    EmployeeId = row.EmployeeId,
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
                ActionLabel = "Review Rejections",
                EmployeeId = row.EmployeeId,
                EmployeeName = row.Name,
                PredictionLabel = "Skill Gap"
            }));
            suggestions.AddRange(topRows.Take(3).Select(row => new SuperAdminSuggestionModel
            {
                Type = "positive",
                Message = $"{row.Name} has high monthly KPIs and collaboration score. Nominate for Employee of the Month?",
                ActionLabel = "Nominate",
                EmployeeId = row.EmployeeId,
                EmployeeName = row.Name,
                PredictionLabel = "Reward Milestone"
            }));

            var heatmap = BuildHeatmap(tasks);
            var points = analyticsRows.Select(row => new CorrelationPoint
            {
                Tasks = row.Tasks,
                Reject = row.RejectPercent
            }).ToList();

            var actual = new List<int> { 20, 22, 17 };
            var projected = new List<int> { 20, 22, 17, 20, 23, 25, 26, 28, 30, 32, 34, 36 };

            var periodKey = string.IsNullOrWhiteSpace(predictionPeriod) ? "month" : predictionPeriod.Trim().ToLowerInvariant();
            if (periodKey != "year")
            {
                periodKey = "month";
            }

            var periodStart = periodKey == "year"
                ? new DateTime(DateTime.Today.Year, 1, 1)
                : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var periodEnd = periodKey == "year"
                ? new DateTime(DateTime.Today.Year, 12, 31)
                : periodStart.AddMonths(1).AddDays(-1);

            var userDirectory = await _context.Users
                .AsNoTracking()
                .Select(dbUser => new
                {
                    dbUser.Id,
                    dbUser.Email,
                    FullName = string.IsNullOrWhiteSpace(dbUser.FullName) ? dbUser.Email : dbUser.FullName,
                    Role = dbUser.Role
                })
                .ToListAsync();

            var employeeProfiles = await _context.Employees
                .AsNoTracking()
                .Select(employee => new
                {
                    employee.user_id,
                    employee.full_name,
                    employee.department,
                    employee.employee_role
                })
                .ToListAsync();

            var predictionRows = new List<SuperAdminPredictionRow>();
            if (_predictionService.IsReady)
            {
                foreach (var userEntry in userDirectory.Where(item =>
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
                    var profile = employeeProfiles.FirstOrDefault(item => item.user_id == userEntry.Id);
                    var roleLabel = profile == null
                        ? "Unassigned"
                        : $"{profile.department} - {profile.employee_role}";

                    predictionRows.Add(new SuperAdminPredictionRow
                    {
                        Name = profile?.full_name ?? userEntry.FullName ?? email,
                        RoleLabel = roleLabel,
                        TotalRequests = totalRequests,
                        TotalApproved = totalApproved,
                        TotalRejected = totalRejected,
                        Prediction = FormatPredictionLabel(prediction.Label),
                        Confidence = prediction.Confidence
                    });
                }
            }

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
                PredictiveProjected = projected,
                PredictionPeriod = periodKey,
                PredictionModelReady = _predictionService.IsReady,
                PredictionRows = predictionRows
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


