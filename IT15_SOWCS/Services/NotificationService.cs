using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        private static string NormalizeRoleKey(string roleKey)
        {
            return (roleKey ?? string.Empty).Trim().ToLowerInvariant();
        }

        private async Task<List<string>> GetRecipientsForRoleKeyAsync(string roleKey)
        {
            var normalizedRole = NormalizeRoleKey(roleKey);
            if (string.IsNullOrWhiteSpace(normalizedRole))
            {
                return new List<string>();
            }

            var userRoleMatches = new List<string>();
            var employeeRoleMatches = new List<string>();

            switch (normalizedRole)
            {
                case "superadmin":
                    userRoleMatches.Add("superadmin");
                    break;
                case "admin":
                    userRoleMatches.Add("admin");
                    break;
                case "manager":
                    employeeRoleMatches.Add("manager");
                    employeeRoleMatches.Add("project manager");
                    userRoleMatches.Add("manager");
                    break;
                case "project manager":
                    employeeRoleMatches.Add("project manager");
                    employeeRoleMatches.Add("manager");
                    break;
                case "hr manager":
                case "hr":
                    employeeRoleMatches.Add("hr manager");
                    employeeRoleMatches.Add("hr");
                    break;
                case "employee":
                    employeeRoleMatches.Add("employee");
                    break;
                default:
                    userRoleMatches.Add(normalizedRole);
                    employeeRoleMatches.Add(normalizedRole);
                    break;
            }

            var recipients = new List<string>();

            if (userRoleMatches.Count > 0)
            {
                var userEmails = await _context.Users
                    .Where(user => user.Email != null &&
                        user.Role != null &&
                        userRoleMatches.Contains(user.Role.Trim().ToLower()))
                    .Select(user => user.Email!)
                    .ToListAsync();
                recipients.AddRange(userEmails);
            }

            if (employeeRoleMatches.Count > 0)
            {
                var employeeEmails = await _context.Employees
                    .Join(_context.Users,
                        employee => employee.user_id,
                        user => user.Id,
                        (employee, user) => new { employee.employee_role, user.Email })
                    .Where(item => item.Email != null &&
                                   item.employee_role != null &&
                                   employeeRoleMatches.Contains(item.employee_role.Trim().ToLower()))
                    .Select(item => item.Email!)
                    .ToListAsync();
                recipients.AddRange(employeeEmails);
            }

            return recipients
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void AddForRecipients(IEnumerable<string> recipients, string title, string message, string category, string? actionUrl)
        {
            foreach (var recipient in recipients)
            {
                _context.Notifications.Add(new NotificationItem
                {
                    recipient_email = recipient.Trim(),
                    title = title.Trim(),
                    message = message.Trim(),
                    category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                    action_url = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim(),
                    created_at = DateTime.UtcNow
                });
            }
        }

        public Task AddForUserAsync(
            string? recipientEmail,
            string title,
            string message,
            string category,
            string? actionUrl = null)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return Task.CompletedTask;
            }

            _context.Notifications.Add(new NotificationItem
            {
                recipient_email = recipientEmail.Trim(),
                title = title.Trim(),
                message = message.Trim(),
                category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                action_url = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim(),
                created_at = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }

        public async Task AddForRoleGroupAsync(
            string roleKey,
            string title,
            string message,
            string category,
            string? actionUrl = null)
        {
            var recipients = await GetRecipientsForRoleKeyAsync(roleKey);
            if (recipients.Count == 0)
            {
                return;
            }

            AddForRecipients(recipients, title, message, category, actionUrl);
        }

        public async Task AddForRoleAsync(
            string role,
            string title,
            string message,
            string category,
            string? actionUrl = null)
        {
            var normalizedRole = (role ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedRole))
            {
                return;
            }

            var recipients = await _context.Users
                .Where(user => user.Email != null && (user.Role ?? string.Empty).ToLower() == normalizedRole)
                .Select(user => user.Email!)
                .Distinct()
                .ToListAsync();

            foreach (var recipient in recipients)
            {
                _context.Notifications.Add(new NotificationItem
                {
                    recipient_email = recipient,
                    title = title.Trim(),
                    message = message.Trim(),
                    category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                    action_url = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim(),
                    created_at = DateTime.UtcNow
                });
            }
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
