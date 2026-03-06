using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Services
{
    public class LeaveBalanceService
    {
        private const decimal AnnualAllocation = 12m;
        private const decimal SickAllocation = 10m;
        private const decimal PersonalAllocation = 5m;

        private readonly AppDbContext _context;

        public LeaveBalanceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task RecomputeAllBalancesAsync()
        {
            var today = DateTime.Today;
            var year = today.Year;
            var month = today.Month;

            var employees = await _context.Employees.ToListAsync();
            if (employees.Count == 0)
            {
                return;
            }

            var approvedLeaves = await _context.LeaveRequests
                .Where(request => request.status != null && request.status.ToLower() == "approved")
                .Select(request => new
                {
                    request.employee_email,
                    request.leave_type,
                    request.days_count,
                    request.start_date
                })
                .ToListAsync();

            var usersByEmail = await _context.Users
                .Where(user => user.Email != null)
                .Select(user => new { user.Id, user.Email })
                .ToListAsync();

            foreach (var employee in employees)
            {
                var employeeEmail = usersByEmail
                    .FirstOrDefault(user => user.Id == employee.user_id)?
                    .Email;

                if (string.IsNullOrWhiteSpace(employeeEmail))
                {
                    continue;
                }

                var annualUsed = approvedLeaves
                    .Where(leave =>
                        string.Equals(leave.employee_email, employeeEmail, StringComparison.OrdinalIgnoreCase) &&
                        NormalizeLeaveType(leave.leave_type) == LeaveBalanceType.Annual &&
                        leave.start_date.Year == year)
                    .Sum(leave => leave.days_count);

                var sickUsed = approvedLeaves
                    .Where(leave =>
                        string.Equals(leave.employee_email, employeeEmail, StringComparison.OrdinalIgnoreCase) &&
                        NormalizeLeaveType(leave.leave_type) == LeaveBalanceType.Sick &&
                        leave.start_date.Year == year &&
                        leave.start_date.Month == month)
                    .Sum(leave => leave.days_count);

                var personalUsed = approvedLeaves
                    .Where(leave =>
                        string.Equals(leave.employee_email, employeeEmail, StringComparison.OrdinalIgnoreCase) &&
                        NormalizeLeaveType(leave.leave_type) == LeaveBalanceType.Personal &&
                        leave.start_date.Year == year &&
                        leave.start_date.Month == month)
                    .Sum(leave => leave.days_count);

                employee.annual_leave_balance = Math.Max(0m, AnnualAllocation - annualUsed);
                employee.sick_leave_balance = Math.Max(0m, SickAllocation - sickUsed);
                employee.personal_leave_balance = Math.Max(0m, PersonalAllocation - personalUsed);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<Employee?> RecomputeBalanceForEmployeeAsync(string employeeEmail)
        {
            if (string.IsNullOrWhiteSpace(employeeEmail))
            {
                return null;
            }

            var user = await _context.Users.FirstOrDefaultAsync(item => item.Email == employeeEmail);
            if (user == null)
            {
                return null;
            }

            var employee = await _context.Employees.FirstOrDefaultAsync(item => item.user_id == user.Id);
            if (employee == null)
            {
                return null;
            }

            var today = DateTime.Today;
            var year = today.Year;
            var month = today.Month;

            var approvedLeaves = await _context.LeaveRequests
                .Where(request =>
                    request.employee_email == employeeEmail &&
                    request.status != null &&
                    request.status.ToLower() == "approved")
                .Select(request => new
                {
                    request.leave_type,
                    request.days_count,
                    request.start_date
                })
                .ToListAsync();

            var annualUsed = approvedLeaves
                .Where(leave =>
                    NormalizeLeaveType(leave.leave_type) == LeaveBalanceType.Annual &&
                    leave.start_date.Year == year)
                .Sum(leave => leave.days_count);

            var sickUsed = approvedLeaves
                .Where(leave =>
                    NormalizeLeaveType(leave.leave_type) == LeaveBalanceType.Sick &&
                    leave.start_date.Year == year &&
                    leave.start_date.Month == month)
                .Sum(leave => leave.days_count);

            var personalUsed = approvedLeaves
                .Where(leave =>
                    NormalizeLeaveType(leave.leave_type) == LeaveBalanceType.Personal &&
                    leave.start_date.Year == year &&
                    leave.start_date.Month == month)
                .Sum(leave => leave.days_count);

            employee.annual_leave_balance = Math.Max(0m, AnnualAllocation - annualUsed);
            employee.sick_leave_balance = Math.Max(0m, SickAllocation - sickUsed);
            employee.personal_leave_balance = Math.Max(0m, PersonalAllocation - personalUsed);

            await _context.SaveChangesAsync();
            return employee;
        }

        public static LeaveBalanceType? NormalizeLeaveType(string? leaveType)
        {
            if (string.IsNullOrWhiteSpace(leaveType))
            {
                return null;
            }

            var normalized = leaveType.Trim().ToLowerInvariant();
            if (normalized.Contains("annual"))
            {
                return LeaveBalanceType.Annual;
            }
            if (normalized.Contains("sick"))
            {
                return LeaveBalanceType.Sick;
            }
            if (normalized.Contains("personal"))
            {
                return LeaveBalanceType.Personal;
            }

            return null;
        }

        public static decimal GetAvailableBalance(Employee employee, LeaveBalanceType leaveType)
        {
            return leaveType switch
            {
                LeaveBalanceType.Annual => employee.annual_leave_balance,
                LeaveBalanceType.Sick => employee.sick_leave_balance,
                LeaveBalanceType.Personal => employee.personal_leave_balance,
                _ => 0m
            };
        }
    }

    public enum LeaveBalanceType
    {
        Annual,
        Sick,
        Personal
    }
}
