using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class EmployeesController : Controller
    {
        private readonly AppDbContext _context;

        public EmployeesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Employees(string? search, string? department)
        {
            var query = _context.Employees.Include(employee => employee.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(employee =>
                    employee.full_name.Contains(search) ||
                    (employee.User != null && employee.User.Email != null && employee.User.Email.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(department) && !department.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(employee => employee.department == department);
            }

            var model = new EmployeesPageViewModel
            {
                Employees = await query.OrderBy(employee => employee.full_name).ToListAsync(),
                Users = await _context.Users.OrderBy(user => user.Email).ToListAsync(),
                Search = search,
                Department = department
            };

            return View("Employees", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string userId,
            string fullName,
            string department,
            string position,
            string contactNumber,
            string employeeRole,
            DateTime? hireDate)
        {
            var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId);
            if (user == null)
            {
                TempData["EmployeesError"] = "Selected user does not exist.";
                return RedirectToAction(nameof(Employees));
            }

            var employee = new Employee
            {
                user_id = user.Id,
                full_name = fullName.Trim(),
                department = department,
                position = position,
                contact_number = contactNumber,
                employee_role = employeeRole,
                hire_date = hireDate ?? DateTime.UtcNow.Date,
                manager_email = User.Identity?.Name,
                annual_leave_balance = 12,
                sick_leave_balance = 10,
                personal_leave_balance = 5,
                is_active = true
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int employeeId,
            string department,
            string position,
            string contactNumber,
            string employeeRole,
            int annualLeave,
            int sickLeave,
            int personalLeave,
            bool isActive)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
            {
                return NotFound();
            }

            employee.department = department;
            employee.position = position;
            employee.contact_number = contactNumber;
            employee.employee_role = employeeRole;
            employee.annual_leave_balance = annualLeave;
            employee.sick_leave_balance = sickLeave;
            employee.personal_leave_balance = personalLeave;
            employee.is_active = isActive;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Employees));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int employeeId)
        {
            var employee = await _context.Employees.Include(item => item.User).FirstOrDefaultAsync(item => item.employee_id == employeeId);
            if (employee == null)
            {
                return NotFound();
            }

            _context.ArchiveItems.Add(new ArchiveItem
            {
                source_id = employee.employee_id,
                source_type = "Employee",
                title = employee.full_name,
                type = "Employee",
                archived_by = User.Identity?.Name ?? "System",
                date_archived = DateTime.UtcNow,
                reason = $"Archived employee record ({employee.User?.Email ?? "no email"})",
                serialized_data = JsonSerializer.Serialize(employee)
            });

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Employees));
        }
    }
}
