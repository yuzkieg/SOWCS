using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class UserManagementController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public UserManagementController(AppDbContext context, UserManager<Users> userManager)
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
        public async Task<IActionResult> UserManagement(string? search, string? filter = "all")
        {
            var usersQuery = _context.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                usersQuery = usersQuery.Where(user =>
                    (user.FullName ?? string.Empty).Contains(search) ||
                    (user.Email ?? string.Empty).Contains(search));
            }

            var activeFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
            var users = await usersQuery.OrderByDescending(user => user.CreatedDate).ToListAsync();

            var employeeRolesByEmail = await _context.Employees
                .Join(_context.Users,
                    employee => employee.user_id,
                    user => user.Id,
                    (employee, user) => new { user.Email, employee.employee_role })
                .Where(item => item.Email != null)
                .ToDictionaryAsync(item => item.Email!, item => item.employee_role);

            var model = new UserManagementPageViewModel
            {
                Users = users,
                TotalUsersCount = await _context.Users.CountAsync(),
                EmployeeRolesByEmail = employeeRolesByEmail,
                ActiveEmployeeUserIds = (await _context.Employees
                    .Where(employee => employee.is_active)
                    .Select(employee => employee.user_id)
                    .Distinct()
                    .ToListAsync())
                    .ToHashSet(),
                AdminCount = await _context.Users.CountAsync(user =>
                    user.Role != null && (
                        user.Role.ToLower() == "admin" ||
                        user.Role.ToLower() == "superadmin")),
                ActiveEmployeeCount = await _context.Employees.CountAsync(employee => employee.is_active),
                Search = search,
                SelectedFilter = activeFilter
            };

            return View("UserManagement", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteUser(string email, string role)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["UserManagementError"] = "Email is required.";
                return RedirectToAction(nameof(UserManagement));
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var existing = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existing != null)
            {
                TempData["UserManagementError"] = "User already exists.";
                return RedirectToAction(nameof(UserManagement));
            }

            var fullName = normalizedEmail.Split('@')[0];
            var user = new Users
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                FullName = fullName,
                Role = role.ToLowerInvariant(),
                EmailConfirmed = true,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, "TempPass123!");
            if (!result.Succeeded)
            {
                TempData["UserManagementError"] = string.Join(" ", result.Errors.Select(error => error.Description));
            }

            return RedirectToAction(nameof(UserManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found." });
            }

            var normalizedRole = (role ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedRole != "admin" && normalizedRole != "user")
            {
                return BadRequest(new { success = false, message = "Invalid role selected." });
            }

            if (string.Equals(user.Role, "superadmin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Super Admin role cannot be changed here." });
            }

            user.Role = normalizedRole;
            user.UpdatedDate = DateTime.UtcNow;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = string.Join(" ", updateResult.Errors.Select(error => error.Description))
                });
            }

            var totalUsersCount = await _context.Users.CountAsync();
            var adminCount = await _context.Users.CountAsync(dbUser =>
                dbUser.Role != null && (
                    dbUser.Role.ToLower() == "admin" ||
                    dbUser.Role.ToLower() == "superadmin"));
            var activeEmployeeCount = await _context.Employees.CountAsync(employee => employee.is_active);

            return Json(new
            {
                success = true,
                role = normalizedRole,
                totalUsersCount,
                adminCount,
                activeEmployeeCount
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (!await IsSuperAdminAsync())
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (string.Equals(user.Email, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["UserManagementError"] = "You cannot delete your currently logged-in user.";
                return RedirectToAction(nameof(UserManagement));
            }

            var userEmail = user.Email ?? string.Empty;

            _context.ArchiveItems.Add(new ArchiveItem
            {
                source_id = null,
                source_type = "User",
                title = string.IsNullOrWhiteSpace(user.FullName) ? (userEmail == string.Empty ? "Unknown User" : userEmail) : user.FullName,
                type = "User",
                archived_by = User.Identity?.Name ?? "System",
                date_archived = DateTime.UtcNow,
                reason = $"Archived user account ({userEmail})",
                serialized_data = JsonSerializer.Serialize(new
                {
                    user.Email,
                    user.FullName,
                    user.Role
                })
            });
            
            var linkedEmployees = await _context.Employees
                .Where(employee => employee.user_id == userId)
                .ToListAsync();

            foreach (var employee in linkedEmployees)
            {
                var employeeSnapshot = new
                {
                    employee.user_id,
                    employee.full_name,
                    employee.department,
                    employee.position,
                    employee.contact_number,
                    employee.hire_date,
                    employee.manager_email,
                    employee.annual_leave_balance,
                    employee.sick_leave_balance,
                    employee.personal_leave_balance,
                    employee.employee_role,
                    employee.is_active
                };

                _context.ArchiveItems.Add(new ArchiveItem
                {
                    source_id = employee.employee_id,
                    source_type = "Employee",
                    title = employee.full_name,
                    type = "Employee",
                    archived_by = User.Identity?.Name ?? "System",
                    date_archived = DateTime.UtcNow,
                    reason = $"Archived together with user account ({userEmail})",
                    serialized_data = JsonSerializer.Serialize(employeeSnapshot)
                });
            }

            foreach (var employee in linkedEmployees)
            {
                employee.is_active = false;
            }

            var hasLeaveRequestReferences = !string.IsNullOrWhiteSpace(userEmail) &&
                await _context.LeaveRequests.AnyAsync(request => request.employee_email == userEmail);
            var hasManagedProjectReferences = !string.IsNullOrWhiteSpace(userEmail) &&
                await _context.Projects.AnyAsync(project => project.manager_email == userEmail);
            var hasLinkedEmployees = linkedEmployees.Count > 0;

            await _context.SaveChangesAsync();

            if (hasLeaveRequestReferences || hasManagedProjectReferences || hasLinkedEmployees)
            {
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                user.UpdatedDate = DateTime.UtcNow;

                var deactivateResult = await _userManager.UpdateAsync(user);
                if (!deactivateResult.Succeeded)
                {
                    TempData["UserManagementError"] = string.Join(" ", deactivateResult.Errors.Select(error => error.Description));
                    return RedirectToAction(nameof(UserManagement));
                }

                TempData["SuccessMessage"] = "User archived and deactivated. Historical records were preserved.";
                return RedirectToAction(nameof(UserManagement));
            }

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                TempData["UserManagementError"] = string.Join(" ", deleteResult.Errors.Select(error => error.Description));
            }
            else
            {
                TempData["SuccessMessage"] = "User archived successfully.";
            }

            return RedirectToAction(nameof(UserManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string userId, bool isActive)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found." });
            }

            if (string.Equals(user.Email, User.Identity?.Name, StringComparison.OrdinalIgnoreCase) && !isActive)
            {
                return BadRequest(new { success = false, message = "You cannot deactivate your currently logged-in user." });
            }

            user.LockoutEnabled = true;
            user.LockoutEnd = isActive
                ? null
                : DateTimeOffset.UtcNow.AddYears(100);
            user.UpdatedDate = DateTime.UtcNow;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return BadRequest(new
                {
                    success = false,
                    message = string.Join(" ", updateResult.Errors.Select(error => error.Description))
                });
            }

            var employeeRecords = await _context.Employees
                .Where(employee => employee.user_id == userId)
                .ToListAsync();

            if (employeeRecords.Count > 0)
            {
                foreach (var employee in employeeRecords)
                {
                    employee.is_active = isActive;
                }

                await _context.SaveChangesAsync();
            }

            var totalUsersCount = await _context.Users.CountAsync();
            var adminCount = await _context.Users.CountAsync(dbUser =>
                dbUser.Role != null && (
                    dbUser.Role.ToLower() == "admin" ||
                    dbUser.Role.ToLower() == "superadmin"));
            var activeEmployeeCount = await _context.Employees.CountAsync(employee => employee.is_active);

            return Json(new
            {
                success = true,
                isActive,
                hasEmployeeRecord = employeeRecords.Count > 0,
                totalUsersCount,
                adminCount,
                activeEmployeeCount
            });
        }
    }
}
