using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class ArchiveController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public ArchiveController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<bool> IsSuperAdminAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return string.Equals(user?.Role, "superadmin", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet]
        public async Task<IActionResult> Archive(string? search, string? type)
        {
            if (!await IsSuperAdminAsync())
            {
                return Forbid();
            }

            ViewData["Title"] = "Archive";

            var expirationCutoff = DateTime.UtcNow.AddDays(-30);
            var expiredItems = await _context.ArchiveItems
                .Where(item => !item.is_restored && item.date_archived <= expirationCutoff)
                .ToListAsync();

            if (expiredItems.Count > 0)
            {
                _context.ArchiveItems.RemoveRange(expiredItems);
                await _context.SaveChangesAsync();
            }

            var query = _context.ArchiveItems
                .Where(item => !item.is_restored)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    item.title.Contains(search) ||
                    item.reason.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(type) && !type.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(item => item.type == type);
            }

            var items = await query
                .OrderByDescending(item => item.date_archived)
                .Select(item => new ArchiveItemModel
                {
                    Id = item.archive_item_id,
                    Title = item.title,
                    Type = item.type,
                    ArchivedBy = item.archived_by,
                    DateArchived = item.date_archived,
                    Reason = item.reason
                })
                .ToListAsync();

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            if (!await IsSuperAdminAsync())
            {
                return Forbid();
            }

            var item = await _context.ArchiveItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(item.serialized_data))
            {
                TempData["ArchiveError"] = "Archived item has no snapshot data and cannot be restored.";
                return RedirectToAction(nameof(Archive));
            }

            try
            {
                switch (item.source_type)
                {
                    case "Project":
                        var project = JsonSerializer.Deserialize<Projects>(item.serialized_data);
                        if (project != null)
                        {
                            project.project_id = 0;
                            _context.Projects.Add(project);
                        }
                        break;
                    case "Task":
                        var task = JsonSerializer.Deserialize<WorkTask>(item.serialized_data);
                        if (task != null)
                        {
                            task.task_id = 0;
                            _context.Tasks.Add(task);
                        }
                        break;
                    case "LeaveRequest":
                        var leave = JsonSerializer.Deserialize<LeaveRequest>(item.serialized_data);
                        if (leave != null)
                        {
                            leave.LR_id = 0;
                            _context.LeaveRequests.Add(leave);
                        }
                        break;
                    case "Employee":
                        var employee = JsonSerializer.Deserialize<Employee>(item.serialized_data);
                        if (employee != null)
                        {
                            employee.employee_id = 0;
                            _context.Employees.Add(employee);
                        }
                        break;
                    case "Document":
                        var document = JsonSerializer.Deserialize<DocumentRecord>(item.serialized_data);
                        if (document != null)
                        {
                            document.document_id = 0;
                            _context.Documents.Add(document);
                        }
                        break;
                    case "AuditLog":
                        if (item.title == "Audit Logs Batch")
                        {
                            var logs = JsonSerializer.Deserialize<List<AuditLogEntry>>(item.serialized_data);
                            if (logs != null)
                            {
                                foreach (var log in logs)
                                {
                                    log.audit_log_id = 0;
                                }
                                _context.AuditLogs.AddRange(logs);
                            }
                        }
                        else
                        {
                            var log = JsonSerializer.Deserialize<AuditLogEntry>(item.serialized_data);
                            if (log != null)
                            {
                                log.audit_log_id = 0;
                                _context.AuditLogs.Add(log);
                            }
                        }
                        break;
                    case "User":
                        var userData = JsonSerializer.Deserialize<ArchivedUserData>(item.serialized_data);
                        if (userData != null && !string.IsNullOrWhiteSpace(userData.Email))
                        {
                            var existingUser = await _userManager.FindByEmailAsync(userData.Email);
                            if (existingUser == null)
                            {
                                var restoredUser = new Users
                                {
                                    UserName = userData.Email,
                                    Email = userData.Email,
                                    FullName = userData.FullName ?? userData.Email,
                                    Role = userData.Role ?? "user",
                                    EmailConfirmed = true,
                                    CreatedDate = DateTime.UtcNow,
                                    UpdatedDate = DateTime.UtcNow
                                };
                                var result = await _userManager.CreateAsync(restoredUser, "TempPass123!");
                                if (!result.Succeeded)
                                {
                                    TempData["ArchiveError"] = string.Join(" ", result.Errors.Select(error => error.Description));
                                    return RedirectToAction(nameof(Archive));
                                }
                            }
                        }
                        break;
                }
            }
            catch
            {
                TempData["ArchiveError"] = "Restore failed due to invalid archived snapshot.";
                return RedirectToAction(nameof(Archive));
            }

            item.is_restored = true;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Archived item restored successfully.";
            return RedirectToAction(nameof(Archive));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await IsSuperAdminAsync())
            {
                return Forbid();
            }

            var item = await _context.ArchiveItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.ArchiveItems.Remove(item);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Archived item deleted permanently.";
            return RedirectToAction(nameof(Archive));
        }
    }

    public class ArchivedUserData
    {
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
    }
}
