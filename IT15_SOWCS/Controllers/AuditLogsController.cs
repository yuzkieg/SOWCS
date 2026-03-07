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
        public async Task<IActionResult> AuditLogs(
            string? search,
            [FromQuery(Name = "action")] string? actionFilter,
            [FromQuery(Name = "from")] DateTime? from,
            [FromQuery(Name = "to")] DateTime? to)
        {
            var query = _context.AuditLogs
                .Where(log => log.action == null || !EF.Functions.Like(log.action, "view"))
                .Where(log => log.action == null || !EF.Functions.Like(log.action, "view%"))
                .AsQueryable();

            if (from.HasValue)
            {
                var fromDate = from.Value.Date;
                query = query.Where(log => log.timestamp >= fromDate);
            }

            if (to.HasValue)
            {
                var toDateExclusive = to.Value.Date.AddDays(1);
                query = query.Where(log => log.timestamp < toDateExclusive);
            }

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

            var logs = await query.OrderByDescending(log => log.timestamp).ToListAsync();
            foreach (var log in logs)
            {
                log.description = NormalizeDescription(log);
            }

            var model = new AuditLogsPageViewModel
            {
                Logs = logs,
                Search = search,
                Action = actionFilter,
                StartDate = from?.Date,
                EndDate = to?.Date
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

        private static string NormalizeDescription(AuditLogEntry log)
        {
            var desc = (log.description ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(desc) ||
                desc.Contains("responded", StringComparison.OrdinalIgnoreCase))
            {
                var action = (log.action ?? string.Empty).Trim().ToLowerInvariant();
                var entity = (log.entity ?? string.Empty).Trim().ToLowerInvariant();
                var descLower = desc.ToLowerInvariant();

                if (descLower.Contains("usermanagement/togglestatus"))
                {
                    return "Updated user status";
                }

                if (descLower.Contains("usermanagement/updaterole"))
                {
                    return "Updated user system role";
                }

                if (descLower.Contains("usermanagement/inviteuser"))
                {
                    return "Invited new user";
                }

                if (descLower.Contains("tasks/move"))
                {
                    return "Moved task status";
                }

                if (descLower.Contains("documents/upload"))
                {
                    return "Uploaded document";
                }

                if (descLower.Contains("archive/restore"))
                {
                    return "Restored archived item";
                }

                if (descLower.Contains("account/logout"))
                {
                    return "Logged out";
                }

                if (descLower.Contains("account/login"))
                {
                    return "Logged in";
                }

                if (descLower.Contains("account/externallogin"))
                {
                    return "Signed in with external login";
                }

                if (descLower.Contains("account/changepassword"))
                {
                    return "Changed account password";
                }

                if (descLower.Contains("account/verifyemail"))
                {
                    return "Sent password verification email";
                }

                if (descLower.Contains("notifications/markasread"))
                {
                    return "Marked notification as read";
                }

                if (descLower.Contains("notifications/markasunread"))
                {
                    return "Marked notification as unread";
                }

                if (descLower.Contains("notifications/clearall"))
                {
                    return "Cleared all notifications";
                }

                if (action is "approve" or "approved")
                {
                    if (entity.Contains("approvals"))
                    {
                        return "Approved request";
                    }

                    return $"Approved {entity} request".Trim();
                }

                if (action is "reject" or "rejected")
                {
                    if (entity.Contains("approvals"))
                    {
                        return "Rejected request";
                    }

                    return $"Rejected {entity} request".Trim();
                }

                if (action == "create")
                {
                    return entity == "projects" ? "Created new project" : $"Created {entity} record".Trim();
                }

                if (action == "update")
                {
                    return $"Updated {entity} record".Trim();
                }

                if (action is "archive" or "delete")
                {
                    return $"Archived {entity} record".Trim();
                }

                if (action == "restore")
                {
                    return "Restored archived item";
                }

                if (action == "invite")
                {
                    return "Invited new user";
                }

                if (action == "upload")
                {
                    return "Uploaded document";
                }

                if (entity == "account" && action == "submit")
                {
                    return "Submitted account action";
                }
            }

            return desc;
        }
    }
}
