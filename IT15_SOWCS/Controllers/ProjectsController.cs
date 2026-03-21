using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class ProjectsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public ProjectsController(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        private static string NormalizePriority(string? priority)
        {
            return (priority ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "low" => "low",
                "medium" => "medium",
                "high" => "high",
                "urgent" => "urgent",
                _ => "medium"
            };
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

        private async Task<bool> IsEmployeeAsync()
        {
            var currentEmail = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentEmail))
            {
                return false;
            }

            var user = await _context.Users.FirstOrDefaultAsync(item => item.Email == currentEmail);
            if (user == null)
            {
                return false;
            }

            var employeeRole = await _context.Employees
                .Where(employee => employee.user_id == user.Id)
                .Select(employee => employee.employee_role)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(employeeRole) &&
                employeeRole.Trim().Equals("employee", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(user.Role) &&
                   user.Role.Trim().Equals("employee", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet]
        public async Task<IActionResult> Projects(string? search, string? status)
        {
            var query = _context.Projects.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(project =>
                    project.name.Contains(search) ||
                    (project.description ?? string.Empty).Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(project => project.status == status);
            }

            var projects = await query.OrderByDescending(project => project.project_id).ToListAsync();
            var projectIds = projects.Select(project => project.project_id).ToList();

            var taskStats = await _context.Tasks
                .Where(task => projectIds.Contains(task.project_id))
                .GroupBy(task => task.project_id)
                .Select(group => new
                {
                    ProjectId = group.Key,
                    TotalCount = group.Count(),
                    CompletedCount = group.Count(task => task.status == "Completed")
                })
                .ToListAsync();

            var progressByProjectId = taskStats.ToDictionary(
                item => item.ProjectId,
                item => item.TotalCount == 0
                    ? 0
                    : (int)Math.Round((item.CompletedCount * 100.0) / item.TotalCount));

            var model = new ProjectsPageViewModel
            {
                Projects = projects,
                Employees = await _context.Employees
                    .Where(employee =>
                        employee.employee_role != null &&
                        (employee.employee_role.ToLower() == "employee" ||
                         employee.employee_role.ToLower() == "project manager" ||
                         employee.employee_role.ToLower() == "manager"))
                    .OrderBy(employee => employee.full_name)
                    .ToListAsync(),
                ProgressByProjectId = progressByProjectId,
                Search = search,
                Status = status,
                CanManageProjects = !await IsEmployeeAsync()
            };

            ViewData["Title"] = "Projects";
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            var teamNames = (project.team_members ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(member => member.Trim())
                .Where(member => !string.IsNullOrWhiteSpace(member))
                .ToList();

            var teamMembers = await _context.Employees
                .Where(employee =>
                    teamNames.Contains(employee.full_name) &&
                    employee.employee_role != null &&
                    (employee.employee_role.ToLower() == "employee" ||
                     employee.employee_role.ToLower() == "project manager" ||
                     employee.employee_role.ToLower() == "manager"))
                .OrderBy(employee => employee.full_name)
                .ToListAsync();

            var tasks = await _context.Tasks
                .Where(task => task.project_id == id)
                .OrderByDescending(task => task.task_id)
                .ToListAsync();

            var model = new ProjectDetailViewModel
            {
                Project = project,
                TeamMembers = teamMembers,
                Tasks = tasks,
                CanManageTasks = !await IsEmployeeAsync()
            };

            ViewData["Title"] = "Project Detail";
            return View("ProjectDetail", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string name,
            string? description,
            string status,
            string priority,
            string[]? teamMembers)
        {
            if (await IsEmployeeAsync())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ProjectsError"] = "Project name is required.";
                return RedirectToAction(nameof(Projects));
            }

            var managerEmail = User.Identity?.Name ?? await _context.Users.Select(user => user.Email).FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(managerEmail))
            {
                TempData["ProjectsError"] = "No manager account available. Create a user first.";
                return RedirectToAction(nameof(Projects));
            }

            var managerName = await _context.Users
                .Where(user => user.Email == managerEmail)
                .Select(user => string.IsNullOrWhiteSpace(user.FullName) ? user.Email! : user.FullName)
                .FirstOrDefaultAsync() ?? managerEmail;

            var allowedTeamMemberNames = await _context.Employees
                .Where(employee =>
                    employee.employee_role != null &&
                    (employee.employee_role.ToLower() == "employee" ||
                     employee.employee_role.ToLower() == "project manager" ||
                     employee.employee_role.ToLower() == "manager"))
                .Select(employee => employee.full_name)
                .ToListAsync();

            var allowedTeamMemberNameSet = allowedTeamMemberNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sanitizedTeamMembers = (teamMembers ?? Array.Empty<string>())
                .Where(member => !string.IsNullOrWhiteSpace(member))
                .Select(member => member.Trim())
                .Where(member => allowedTeamMemberNameSet.Contains(member))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var project = new Projects
            {
                name = name.Trim(),
                description = description?.Trim(),
                status = status,
                priority = NormalizePriority(priority),
                manager_email = managerEmail,
                manager_name = managerName,
                start_date = DateTime.UtcNow.Date,
                due_date = DateTime.UtcNow.Date.AddDays(30),
                team_members = string.Join(", ", sanitizedTeamMembers),
                progress = 0
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            await _notificationService.AddForRoleGroupAsync(
                "superadmin",
                "New Project Created",
                $"{project.name} was created and added to the projects list.",
                "Project",
                "/Projects/Projects");

            if (sanitizedTeamMembers.Count > 0)
            {
                var teamMemberEmails = await _context.Employees
                    .Join(_context.Users,
                        employee => employee.user_id,
                        user => user.Id,
                        (employee, user) => new { employee.full_name, user.Email })
                    .Where(item => item.Email != null &&
                                   sanitizedTeamMembers.Contains(item.full_name))
                    .Select(item => item.Email!)
                    .Distinct()
                    .ToListAsync();

                foreach (var email in teamMemberEmails)
                {
                    await _notificationService.AddForUserAsync(
                        email,
                        "New Project Assignment",
                        $"You were added to project \"{project.name}\".",
                        "Project",
                        $"/Projects/Detail/{project.project_id}");
                }
            }

            TempData["SuccessMessage"] = "Project created successfully.";
            return RedirectToAction(nameof(Projects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int projectId,
            string name,
            string? description,
            string status,
            string priority,
            int progress,
            string[]? teamMembers)
        {
            if (await IsEmployeeAsync())
            {
                return Forbid();
            }

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound();
            }

            project.name = name.Trim();
            project.description = description?.Trim();
            project.status = status;
            project.priority = NormalizePriority(priority);
            project.progress = Math.Clamp(progress, 0, 100);

            var allowedTeamMemberNames = await _context.Employees
                .Where(employee =>
                    employee.employee_role != null &&
                    (employee.employee_role.ToLower() == "employee" ||
                     employee.employee_role.ToLower() == "project manager" ||
                     employee.employee_role.ToLower() == "manager"))
                .Select(employee => employee.full_name)
                .ToListAsync();

            var allowedTeamMemberNameSet = allowedTeamMemberNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sanitizedTeamMembers = (teamMembers ?? Array.Empty<string>())
                .Where(member => !string.IsNullOrWhiteSpace(member))
                .Select(member => member.Trim())
                .Where(member => allowedTeamMemberNameSet.Contains(member))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            project.team_members = string.Join(", ", sanitizedTeamMembers);

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Project updated successfully.";
            return RedirectToAction(nameof(Projects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int projectId)
        {
            if (!await IsSuperAdminAsync())
            {
                return Forbid();
            }

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound();
            }

            var relatedTasks = await _context.Tasks
                .Where(task => task.project_id == projectId)
                .ToListAsync();

            var projectSnapshot = new
            {
                project.project_id,
                project.name,
                project.description,
                project.status,
                project.priority,
                project.progress,
                project.team_members,
                project.manager_name,
                project.manager_email,
                project.start_date,
                project.due_date
            };

            var taskArchiveReason = $"Archived with project:{project.project_id}";
            foreach (var task in relatedTasks)
            {
                var taskSnapshot = new
                {
                    task.task_id,
                    task.project_id,
                    task.employee_id,
                    task.title,
                    task.description,
                    task.project_name,
                    task.assigned_to,
                    task.assigned_name,
                    task.priority,
                    task.status,
                    task.due_date,
                    task.completed_date
                };

                _context.ArchiveItems.Add(new ArchiveItem
                {
                    source_id = task.task_id,
                    source_type = "Task",
                    title = task.title,
                    type = "Task",
                    archived_by = User.Identity?.Name ?? "System",
                    date_archived = DateTime.UtcNow,
                    reason = taskArchiveReason,
                    serialized_data = JsonSerializer.Serialize(taskSnapshot)
                });
            }

            _context.ArchiveItems.Add(new ArchiveItem
            {
                source_id = project.project_id,
                source_type = "Project",
                title = project.name,
                type = "Project",
                archived_by = User.Identity?.Name ?? "System",
                date_archived = DateTime.UtcNow,
                reason = "Archived from Projects module",
                serialized_data = JsonSerializer.Serialize(projectSnapshot)
            });

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Project archived successfully.";
            return RedirectToAction(nameof(Projects));
        }
    }
}


