using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.Models
{
    public class NotificationItem
    {
        [Key]
        public int notification_id { get; set; }

        [Required]
        [EmailAddress]
        public string recipient_email { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string title { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string message { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? action_url { get; set; }

        [MaxLength(40)]
        public string category { get; set; } = "General";

        public bool is_read { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;
    }
}
