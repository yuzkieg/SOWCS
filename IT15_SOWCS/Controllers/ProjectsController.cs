using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class ProjectsController : Controller
    {
        private readonly AppDbContext _context;

        public ProjectsController(AppDbContext context)
        {
            _context = context;
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
                Employees = await _context.Employees.OrderBy(employee => employee.full_name).ToListAsync(),
                ProgressByProjectId = progressByProjectId,
                Search = search,
                Status = status
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
                .Where(employee => teamNames.Contains(employee.full_name))
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
                Tasks = tasks
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
            string[] teamMembers)
        {
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

            var project = new Projects
            {
                name = name.Trim(),
                description = description?.Trim(),
                status = status,
                priority = priority,
                manager_email = managerEmail,
                manager_name = managerName,
                start_date = DateTime.UtcNow.Date,
                due_date = DateTime.UtcNow.Date.AddDays(30),
                team_members = string.Join(", ", teamMembers.Where(member => !string.IsNullOrWhiteSpace(member))),
                progress = 0
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();
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
            int progress)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound();
            }

            project.name = name.Trim();
            project.description = description?.Trim();
            project.status = status;
            project.priority = priority;
            project.progress = Math.Clamp(progress, 0, 100);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Projects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int projectId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound();
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
                serialized_data = JsonSerializer.Serialize(project)
            });

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Projects));
        }
    }
}
