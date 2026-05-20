using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
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
            if (IsPublicController(controller))
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

            if (PasswordPolicyService.IsPasswordExpired(user) &&
                !string.Equals(controller, "profile", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToActionResult("Profile", "Profile", new
                {
                    passwordExpired = true
                });
                return;
            }

            if (!user.TwoFactorEnabled &&
                await MfaPolicyService.IsMfaRequiredAsync(user, _context) &&
                !string.Equals(controller, "profile", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToActionResult("Profile", "Profile", new
                {
                    requireMfaSetup = true
                });
                return;
            }

            var isSuperAdmin = string.Equals(user.Role, "superadmin", StringComparison.OrdinalIgnoreCase);
            if (isSuperAdmin)
            {
                await next();
                return;
            }

            var accessContext = await BuildAccessContextAsync(user);
            var isAllowed = IsAllowed(controller, accessContext);

            if (!isAllowed)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }

            await next();

        }

        private static bool IsPublicController(string controller)
        {
            return controller == "account" || controller == "home";
        }

        private async Task<AccessContext> BuildAccessContextAsync(Users user)
        {
            var employeeRole = await _context.Employees
                .Where(employee => employee.user_id == user.Id)
                .Select(employee => employee.employee_role)
                .FirstOrDefaultAsync() ?? string.Empty;

            var normalizedEmployeeRole = employeeRole.Trim().ToLowerInvariant();
            return new AccessContext(
                string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase),
                normalizedEmployeeRole == "hr" || normalizedEmployeeRole == "hr manager",
                normalizedEmployeeRole == "manager" || normalizedEmployeeRole == "project manager");
        }

        private static bool IsAllowed(string controller, AccessContext accessContext)
        {
            return controller switch
            {
                "usermanagement" => accessContext.IsAdmin,
                "auditlogs" => accessContext.IsAdmin,
                "archive" => false,
                "employees" => accessContext.IsAdmin || accessContext.IsHrManager,
                "reports" => accessContext.IsAdmin || accessContext.IsHrManager || accessContext.IsProjectManager,
                "approvals" => accessContext.IsAdmin || accessContext.IsHrManager || accessContext.IsProjectManager,
                "dashboard" => true,
                "projects" => true,
                "tasks" => true,
                "documents" => true,
                "leaverequest" => true,
                "profile" => true,
                "notifications" => true,
                _ => true
            };
        }

        private sealed record AccessContext(bool IsAdmin, bool IsHrManager, bool IsProjectManager);
    }
}
