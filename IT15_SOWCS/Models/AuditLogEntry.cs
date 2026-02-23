using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.Models
{
    public class AuditLogEntry
    {
        [Key]
        public int audit_log_id { get; set; }

        public DateTime timestamp { get; set; } = DateTime.UtcNow;

        public string user_name { get; set; } = string.Empty;

        [EmailAddress]
        public string user_email { get; set; } = string.Empty;

        public string action { get; set; } = string.Empty;

        public string entity { get; set; } = string.Empty;

        public string description { get; set; } = string.Empty;
    }
}
