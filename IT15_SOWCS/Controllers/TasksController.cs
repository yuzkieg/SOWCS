using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class TasksController : Controller
    {
        private readonly AppDbContext _context;

        public TasksController(AppDbContext context)
        {
            _context = context;
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
            DateTime dueDate)
        {
            var employee = await _context.Employees.Include(item => item.User).FirstOrDefaultAsync(item => item.employee_id == employeeId);
            var project = await _context.Projects.FirstOrDefaultAsync(item => item.project_id == projectId);

            if (employee == null || project == null || string.IsNullOrWhiteSpace(title))
            {
                TempData["TasksError"] = "Invalid task data.";
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
            await _context.SaveChangesAsync();
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
            DateTime dueDate)
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

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Tasks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int taskId)
        {
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
            return RedirectToAction(nameof(Tasks));
        }
    }
}
