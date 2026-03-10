using IT15_SOWCS.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Controllers
{
    [Authorize]
    [Route("notifications")]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _context;

        public NotificationsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var notifications = await _context.Notifications
                .Where(item => item.recipient_email == email)
                .OrderByDescending(item => item.created_at)
                .Take(40)
                .Select(item => new
                {
                    id = item.notification_id,
                    title = item.title,
                    message = item.message,
                    category = item.category,
                    actionUrl = item.action_url,
                    isRead = item.is_read,
                    createdAt = item.created_at
                })
                .ToListAsync();

            var unreadCount = notifications.Count(item => !item.isRead);
            return Json(new
            {
                success = true,
                unreadCount,
                notifications
            });
        }

        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllRead()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            await _context.Notifications
                .Where(item => item.recipient_email == email && !item.is_read)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.is_read, true));

            return Json(new { success = true });
        }

        [HttpPost("{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var affectedRows = await _context.Notifications
                .Where(item => item.notification_id == id && item.recipient_email == email)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.is_read, true));

            if (affectedRows == 0)
            {
                return NotFound(new { success = false });
            }

            return Json(new { success = true });
        }

        [HttpPost("{id:int}/unread")]
        public async Task<IActionResult> MarkUnread(int id)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var affectedRows = await _context.Notifications
                .Where(item => item.notification_id == id && item.recipient_email == email)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.is_read, false));

            if (affectedRows == 0)
            {
                return NotFound(new { success = false });
            }

            return Json(new { success = true });
        }

        [HttpPost("{id:int}/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(item => item.notification_id == id && item.recipient_email == email);
            if (notification == null)
            {
                return NotFound(new { success = false });
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
