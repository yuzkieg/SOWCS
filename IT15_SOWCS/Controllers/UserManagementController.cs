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
                return NotFound();
            }

            user.Role = role.ToLowerInvariant();
            user.UpdatedDate = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return RedirectToAction(nameof(UserManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
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

            _context.ArchiveItems.Add(new ArchiveItem
            {
                source_id = null,
                source_type = "User",
                title = string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? "Unknown User") : user.FullName,
                type = "User",
                archived_by = User.Identity?.Name ?? "System",
                date_archived = DateTime.UtcNow,
                reason = $"Archived user account ({user.Email})",
                serialized_data = JsonSerializer.Serialize(new
                {
                    user.Email,
                    user.FullName,
                    user.Role
                })
            });
            await _context.SaveChangesAsync();

            await _userManager.DeleteAsync(user);
            return RedirectToAction(nameof(UserManagement));
        }
    }
}
