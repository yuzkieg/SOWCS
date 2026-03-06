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
