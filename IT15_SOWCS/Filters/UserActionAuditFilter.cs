using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;

namespace IT15_SOWCS.Filters
{
    public class UserActionAuditFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _context;

        public UserActionAuditFilter(AppDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();

            if (executedContext.Exception != null)
            {
                return;
            }

            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                return;
            }

            if (string.Equals(descriptor.ControllerName, "Account", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(descriptor.ActionName, "Login", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(descriptor.ActionName, "ExternalLogin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(descriptor.ActionName, "ExternalLoginCallback", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(descriptor.ActionName, "Logout", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var request = context.HttpContext.Request;
            var method = request.Method.ToUpperInvariant();
            var controller = descriptor.ControllerName;
            var actionName = descriptor.ActionName;
            var route = $"{controller}/{actionName}";
            var actionLabel = ResolveActionLabel(method, actionName, context.ActionArguments);
            var description = await ResolveDescriptionAsync(controller, actionName, actionLabel, context.ActionArguments, route, method, context.HttpContext.Response?.StatusCode ?? 0);

            var userEmail = context.HttpContext.User?.Identity?.Name ?? "anonymous@local";
            var userName = userEmail;
            if (!string.Equals(userEmail, "anonymous@local", StringComparison.OrdinalIgnoreCase))
            {
                var dbUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Email == userEmail);
                if (dbUser != null && !string.IsNullOrWhiteSpace(dbUser.FullName))
                {
                    userName = dbUser.FullName;
                }
            }
            var ipAddress = ResolveClientIp(context.HttpContext);
            var severity = ResolveSeverity(actionLabel, controller, actionName);

            _context.AuditLogs.Add(new AuditLogEntry
            {
                timestamp = DateTime.UtcNow,
                user_email = userEmail,
                user_name = userName,
                action = actionLabel,
                entity = controller,
                severity = severity,
                ip_address = ipAddress,
                description = description
            });

            await _context.SaveChangesAsync();
        }

        private static string ResolveActionLabel(string method, string actionName, IDictionary<string, object?> args)
        {
            if (method == "GET")
            {
                return "view";
            }

            if (method == "DELETE")
            {
                return "delete";
            }

            if (method == "PUT" || method == "PATCH")
            {
                return "update";
            }

            var action = actionName.ToLowerInvariant();
            if (action.Contains("upload"))
            {
                return "upload";
            }

            if (action.Contains("move"))
            {
                return "update";
            }

            if (action.Contains("toggle"))
            {
                return "update";
            }

            if (action.Contains("invite"))
            {
                return "invite";
            }

            if (action.Contains("create"))
            {
                return "create";
            }

            if (action.Contains("restore"))
            {
                return "restore";
            }

            if (action.Contains("delete"))
            {
                return "archive";
            }

            if (args.TryGetValue("status", out var statusArg) && statusArg is string status)
            {
                if (status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    return "approved";
                }

                if (status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                {
                    return "rejected";
                }
            }

            if (action.Contains("update"))
            {
                return "update";
            }

            if (method == "POST")
            {
                return "submit";
            }

            return method.ToLowerInvariant();
        }

        private async Task<string> ResolveDescriptionAsync(
            string controller,
            string actionName,
            string actionLabel,
            IDictionary<string, object?> args,
            string route,
            string method,
            int statusCode)
        {
            if (controller.Equals("Approvals", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("UpdateLeaveStatus", StringComparison.OrdinalIgnoreCase))
            {
                var status = GetStringArg(args, "status");
                var leaveId = GetIntArg(args, "id");
                if (leaveId.HasValue)
                {
                    var leave = await _context.LeaveRequests.FindAsync(leaveId.Value);
                    if (leave != null)
                    {
                        var decision = ToPastTense(status);
                        if (!string.IsNullOrWhiteSpace(leave.leave_type) && !string.IsNullOrWhiteSpace(leave.employee_name))
                        {
                            return $"{decision} {leave.leave_type.ToLowerInvariant()} leave request for {leave.employee_name}";
                        }
                    }
                }

                return $"{ToPastTense(status)} leave request";
            }

            if (controller.Equals("Approvals", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("UpdateDocumentStatus", StringComparison.OrdinalIgnoreCase))
            {
                return $"{ToPastTense(GetStringArg(args, "status"))} document request";
            }

            if (controller.Equals("Projects", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("Create", StringComparison.OrdinalIgnoreCase))
            {
                var projectName = GetStringArg(args, "name");
                return string.IsNullOrWhiteSpace(projectName)
                    ? "Created new project"
                    : $"Created new project: {projectName}";
            }

            if (controller.Equals("Employees", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("Update", StringComparison.OrdinalIgnoreCase))
            {
                var employeeId = GetIntArg(args, "employeeId");
                var employee = employeeId.HasValue ? await _context.Employees.FindAsync(employeeId.Value) : null;
                return employee == null
                    ? "Updated employee record"
                    : $"Updated employee role for {employee.full_name}";
            }

            if (controller.Equals("UserManagement", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("ToggleStatus", StringComparison.OrdinalIgnoreCase))
            {
                var isActive = GetBoolArg(args, "isActive");
                return isActive.HasValue
                    ? (isActive.Value ? "Activated user account" : "Deactivated user account")
                    : "Updated user status";
            }

            if (controller.Equals("UserManagement", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("UpdateRole", StringComparison.OrdinalIgnoreCase))
            {
                return "Updated user system role";
            }

            if (controller.Equals("Tasks", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("Move", StringComparison.OrdinalIgnoreCase))
            {
                var status = GetStringArg(args, "status");
                return string.IsNullOrWhiteSpace(status)
                    ? "Moved task status"
                    : $"Moved task to {status}";
            }

            if (controller.Equals("UserManagement", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("InviteUser", StringComparison.OrdinalIgnoreCase))
            {
                return "Invited new user";
            }

            if (controller.Equals("Documents", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("Upload", StringComparison.OrdinalIgnoreCase))
            {
                return "Uploaded document";
            }

            if (controller.Equals("Archive", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("Restore", StringComparison.OrdinalIgnoreCase))
            {
                return "Restored archived item";
            }

            if (controller.Equals("Account", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("Logout", StringComparison.OrdinalIgnoreCase))
            {
                return "Logged out";
            }

            if (controller.Equals("Account", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("ExternalLogin", StringComparison.OrdinalIgnoreCase))
            {
                return "Signed in with Google";
            }

            if (controller.Equals("Account", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("Login", StringComparison.OrdinalIgnoreCase))
            {
                return "Logged in";
            }

            if (controller.Equals("Account", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("ChangePassword", StringComparison.OrdinalIgnoreCase))
            {
                return "Changed account password";
            }

            if (controller.Equals("Account", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("VerifyEmail", StringComparison.OrdinalIgnoreCase))
            {
                return "Sent password verification email";
            }

            if (controller.Equals("Notifications", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("MarkAsRead", StringComparison.OrdinalIgnoreCase))
            {
                return "Marked notification as read";
            }

            if (controller.Equals("Notifications", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("MarkAsUnread", StringComparison.OrdinalIgnoreCase))
            {
                return "Marked notification as unread";
            }

            if (controller.Equals("Notifications", StringComparison.OrdinalIgnoreCase) &&
                actionName.Equals("ClearAll", StringComparison.OrdinalIgnoreCase))
            {
                return "Cleared all notifications";
            }

            if (actionLabel.Equals("approved", StringComparison.OrdinalIgnoreCase))
            {
                return $"Approved {controller.ToLowerInvariant()} request";
            }

            if (actionLabel.Equals("rejected", StringComparison.OrdinalIgnoreCase))
            {
                return $"Rejected {controller.ToLowerInvariant()} request";
            }

            if (actionLabel.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                return $"Created new {SplitPascal(controller).ToLowerInvariant()} record";
            }

            if (actionLabel.Equals("update", StringComparison.OrdinalIgnoreCase))
            {
                return $"Updated {SplitPascal(controller).ToLowerInvariant()} record";
            }

            if (actionLabel.Equals("archive", StringComparison.OrdinalIgnoreCase) || actionLabel.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                return $"Archived {SplitPascal(controller).ToLowerInvariant()} record";
            }

            if (actionLabel.Equals("submit", StringComparison.OrdinalIgnoreCase))
            {
                return $"Submitted {SplitPascal(controller).ToLowerInvariant()} action";
            }

            return $"{route} [{method}] responded {statusCode}";
        }

        private static string? GetStringArg(IDictionary<string, object?> args, string key)
        {
            return args.TryGetValue(key, out var value) ? value?.ToString()?.Trim() : null;
        }

        private static int? GetIntArg(IDictionary<string, object?> args, string key)
        {
            if (!args.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
        }

        private static bool? GetBoolArg(IDictionary<string, object?> args, string key)
        {
            if (!args.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            return bool.TryParse(value.ToString(), out var parsed) ? parsed : null;
        }

        private static string ToPastTense(string? status)
        {
            if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                return "Approved";
            }

            if (string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                return "Rejected";
            }

            return string.IsNullOrWhiteSpace(status)
                ? "Updated"
                : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(status.Trim().ToLowerInvariant());
        }

        private static string SplitPascal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "record";
            }

            return string.Concat(value.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()));
        }

        private static string ResolveClientIp(HttpContext context)
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        }

        private static string ResolveSeverity(string actionLabel, string controller, string actionName)
        {
            var action = (actionLabel ?? string.Empty).Trim().ToLowerInvariant();
            var entity = (controller ?? string.Empty).Trim().ToLowerInvariant();
            var actionLower = (actionName ?? string.Empty).Trim().ToLowerInvariant();

            if (action == "login_failed")
            {
                return "Warning";
            }

            if (action is "archive" or "delete")
            {
                return "Major";
            }

            if (action is "restore")
            {
                return "Major";
            }

            if (action is "approved" or "rejected")
            {
                return "Major";
            }

            if (action is "update")
            {
                return "Major";
            }

            if (action is "create" or "invite" or "upload")
            {
                return "Minor";
            }

            if (entity == "account" && (actionLower.Contains("login") || actionLower.Contains("logout")))
            {
                return "Informational";
            }

            return "Informational";
        }
    }
}
