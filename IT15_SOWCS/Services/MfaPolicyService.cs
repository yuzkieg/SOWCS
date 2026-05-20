using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Services
{
    public static class MfaPolicyService
    {
        public static async Task<bool> IsMfaRequiredAsync(Users user, AppDbContext context)
        {
            if (string.Equals(user.Role, "superadmin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var employeeRole = await context.Employees
                .Where(employee => employee.user_id == user.Id)
                .Select(employee => employee.employee_role)
                .FirstOrDefaultAsync() ?? string.Empty;

            var normalizedEmployeeRole = employeeRole.Trim().ToLowerInvariant();
            return normalizedEmployeeRole == "hr" ||
                   normalizedEmployeeRole == "hr manager" ||
                   normalizedEmployeeRole == "manager" ||
                   normalizedEmployeeRole == "project manager";
        }

        public static string GetRequiredMessage()
        {
            return "Multi-factor authentication is required for your role before you can use the rest of the system.";
        }
    }
}
