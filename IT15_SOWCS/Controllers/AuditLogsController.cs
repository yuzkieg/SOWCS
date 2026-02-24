using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class AuditLogsController : Controller
    {
        private readonly AppDbContext _context;

        public AuditLogsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> AuditLogs(string? search, [FromQuery(Name = "action")] string? actionFilter)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(log =>
                    log.user_name.Contains(search) ||
                    log.user_email.Contains(search) ||
                    log.description.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(actionFilter) && !actionFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedAction = actionFilter.Trim().ToLowerInvariant();
                var acceptedActions = normalizedAction switch
                {
                    "approve" => new[] { "approve", "approved" },
                    "reject" => new[] { "reject", "rejected" },
                    "delete" => new[] { "delete", "archive" },
                    _ => new[] { normalizedAction }
                };

                query = query.Where(log => log.action != null && acceptedActions.Contains(log.action.ToLower()));
            }

            var model = new AuditLogsPageViewModel
            {
                Logs = await query.OrderByDescending(log => log.timestamp).ToListAsync(),
                Search = search,
                Action = actionFilter
            };

            return View("AuditLogs", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var log = await _context.AuditLogs.FindAsync(id);
            if (log == null)
            {
                return NotFound();
            }

            _context.ArchiveItems.Add(new Models.ArchiveItem
            {
                source_id = log.audit_log_id,
                source_type = "AuditLog",
                title = $"{log.action} {log.entity}",
                type = "AuditLog",
                archived_by = User.Identity?.Name ?? "System",
                date_archived = DateTime.UtcNow,
                reason = $"Archived audit log entry #{log.audit_log_id}",
                serialized_data = JsonSerializer.Serialize(log)
            });

            _context.AuditLogs.Remove(log);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(AuditLogs));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAll()
        {
            var logs = await _context.AuditLogs.ToListAsync();
            if (logs.Count > 0)
            {
                _context.ArchiveItems.Add(new Models.ArchiveItem
                {
                    source_id = null,
                    source_type = "AuditLog",
                    title = "Audit Logs Batch",
                    type = "AuditLog",
                    archived_by = User.Identity?.Name ?? "System",
                    date_archived = DateTime.UtcNow,
                    reason = $"Archived all audit logs ({logs.Count} entries)",
                    serialized_data = JsonSerializer.Serialize(logs)
                });
            }

            _context.AuditLogs.RemoveRange(logs);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(AuditLogs));
        }
    }
}
