using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class TasksController : Controller
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public TasksController(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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
        public async Task<IActionResult> Tasks(string? search, string? status, string? priority)
        {
            var query = _context.Tasks
                .Include(task => task.Employee)
                .Include(task => task.Project)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(task =>
                    task.title.Contains(search) ||
                    (task.description ?? string.Empty).Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(task => task.status == status);
            }

            if (!string.IsNullOrWhiteSpace(priority) && !priority.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(task => task.priority == priority);
            }

            var model = new TasksPageViewModel
            {
                Tasks = await query.OrderByDescending(task => task.task_id).ToListAsync(),
                Employees = await _context.Employees.OrderBy(employee => employee.full_name).ToListAsync(),
                Projects = await _context.Projects.OrderBy(project => project.name).ToListAsync(),
                Search = search,
                Status = status,
                Priority = priority
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int employeeId,
            int projectId,
            string title,
            string? description,
            string status,
            string priority,
            DateTime dueDate,
            int? redirectProjectId)
        {
            var employee = await _context.Employees.Include(item => item.User).FirstOrDefaultAsync(item => item.employee_id == employeeId);
            var project = await _context.Projects.FirstOrDefaultAsync(item => item.project_id == projectId);

            if (employee == null || project == null || string.IsNullOrWhiteSpace(title))
            {
                TempData["TasksError"] = "Invalid task data.";
                return RedirectToAction(nameof(Tasks));
            }

            if (dueDate.Date < DateTime.Today)
            {
                TempData["TasksError"] = "Due date cannot be in the past.";
                if (redirectProjectId.HasValue)
                {
                    return RedirectToAction("Detail", "Projects", new { id = redirectProjectId.Value });
                }

                return RedirectToAction(nameof(Tasks));
            }

            var task = new WorkTask
            {
                employee_id = employee.employee_id,
                project_id = project.project_id,
                title = title.Trim(),
                description = description?.Trim(),
                status = status,
                priority = priority,
                due_date = dueDate,
                project_name = project.name,
                assigned_to = employee.User?.Email ?? string.Empty,
                assigned_name = employee.full_name
            };

            _context.Tasks.Add(task);
            await _notificationService.AddForUserAsync(
                task.assigned_to,
                "New Task Assigned",
                $"You were assigned \"{task.title}\" in project {project.name}.",
                "Task",
                $"/Projects/Detail/{project.project_id}");
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Task created successfully.";

            if (redirectProjectId.HasValue)
            {
                return RedirectToAction("Detail", "Projects", new { id = redirectProjectId.Value });
            }

            return RedirectToAction(nameof(Tasks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int taskId,
            string title,
            string? description,
            string status,
            string priority,
            DateTime dueDate,
            int? employeeId,
            int? redirectProjectId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null)
            {
                return NotFound();
            }

            task.title = title.Trim();
            task.description = description?.Trim();
            task.status = status;
            task.priority = priority;
            task.due_date = dueDate;
            task.completed_date = status == "Completed" ? DateTime.UtcNow : null;

            if (employeeId.HasValue)
            {
                var previousAssignedTo = task.assigned_to;
                var employee = await _context.Employees
                    .Include(item => item.User)
                    .FirstOrDefaultAsync(item => item.employee_id == employeeId.Value);

                if (employee != null)
                {
                    task.employee_id = employee.employee_id;
                    task.assigned_name = employee.full_name;
                    task.assigned_to = employee.User?.Email ?? string.Empty;

                    if (!string.Equals(previousAssignedTo, task.assigned_to, StringComparison.OrdinalIgnoreCase))
                    {
                        await _notificationService.AddForUserAsync(
                            task.assigned_to,
                            "Task Reassigned",
                            $"Task \"{task.title}\" was assigned to you.",
                            "Task",
                            $"/Projects/Detail/{task.project_id}");
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Task updated successfully.";

            if (redirectProjectId.HasValue)
            {
                return RedirectToAction("Detail", "Projects", new { id = redirectProjectId.Value });
            }

            return RedirectToAction(nameof(Tasks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Move(int taskId, string status, int? redirectProjectId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null)
            {
                return NotFound();
            }

            task.status = status;
            task.completed_date = status == "Completed" ? DateTime.UtcNow : null;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Task moved to {status}.";

            if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    success = true,
                    status,
                    message = TempData["SuccessMessage"]?.ToString() ?? "Task status updated."
                });
            }

            if (redirectProjectId.HasValue)
            {
                return RedirectToAction("Detail", "Projects", new { id = redirectProjectId.Value });
            }

            return RedirectToAction(nameof(Tasks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int taskId, int? redirectProjectId)
        {
            if (!await IsSuperAdminAsync())
            {
                return Forbid();
            }

            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null)
            {
                return NotFound();
            }

            _context.ArchiveItems.Add(new ArchiveItem
            {
                source_id = task.task_id,
                source_type = "Task",
                title = task.title,
                type = "Task",
                archived_by = User.Identity?.Name ?? "System",
                date_archived = DateTime.UtcNow,
                reason = "Archived from Tasks module",
                serialized_data = JsonSerializer.Serialize(task)
            });

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Task archived successfully.";

            if (redirectProjectId.HasValue)
            {
                return RedirectToAction("Detail", "Projects", new { id = redirectProjectId.Value });
            }

            return RedirectToAction(nameof(Tasks));
        }
    }
}
