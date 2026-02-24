using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

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

            var request = context.HttpContext.Request;
            var method = request.Method.ToUpperInvariant();
            var controller = descriptor.ControllerName;
            var actionName = descriptor.ActionName;
            var route = $"{controller}/{actionName}";
            var actionLabel = ResolveActionLabel(method, actionName, context.ActionArguments);

            var userEmail = context.HttpContext.User?.Identity?.Name ?? "anonymous@local";
            var statusCode = context.HttpContext.Response?.StatusCode ?? 0;

            _context.AuditLogs.Add(new AuditLogEntry
            {
                timestamp = DateTime.UtcNow,
                user_email = userEmail,
                user_name = userEmail,
                action = actionLabel,
                entity = controller,
                description = $"{route} [{method}] responded {statusCode}"
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
    }
}
