using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Filters
{
    public class ModuleAccessFilter : IAsyncActionFilter
    {
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _context;

        public ModuleAccessFilter(UserManager<Users> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpUser = context.HttpContext.User;
            if (httpUser?.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            var controller = (context.RouteData.Values["controller"]?.ToString() ?? string.Empty).ToLowerInvariant();
            if (controller == "account" || controller == "home")
            {
                await next();
                return;
            }

            var user = await _userManager.GetUserAsync(httpUser);
            if (user == null)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new
                {
                    inactiveEmail = user.Email
                });
                return;
            }

            var isSuperAdmin = string.Equals(user.Role, "superadmin", StringComparison.OrdinalIgnoreCase);
            if (isSuperAdmin)
            {
                await next();
                return;
            }

            var isAdmin = string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase);
            var employeeRole = await _context.Employees
                .Where(employee => employee.user_id == user.Id)
                .Select(employee => employee.employee_role)
                .FirstOrDefaultAsync() ?? string.Empty;

            var normalizedEmployeeRole = employeeRole.Trim().ToLowerInvariant();
            var isHrManager = normalizedEmployeeRole == "hr" || normalizedEmployeeRole == "hr manager";
            var isProjectManager = normalizedEmployeeRole == "manager" || normalizedEmployeeRole == "project manager";

            var isAllowed = controller switch
            {
                "usermanagement" => isAdmin,
                "auditlogs" => isAdmin,
                "archive" => false,
                "employees" => isAdmin || isHrManager,
                "reports" => isAdmin || isHrManager || isProjectManager,
                "approvals" => isAdmin || isHrManager || isProjectManager,
                "dashboard" => true,
                "projects" => true,
                "tasks" => true,
                "documents" => true,
                "leaverequest" => true,
                "profile" => true,
                "notifications" => true,
                _ => true
            };

            if (!isAllowed)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }

            await next();

        }
    }
}

