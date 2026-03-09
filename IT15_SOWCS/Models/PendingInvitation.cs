using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.Models
{
    public class PendingInvitation
    {
        [Key]
        public int invitation_id { get; set; }

        [Required]
        [EmailAddress]
        public string email { get; set; } = string.Empty;

        [Required]
        public string role { get; set; } = "user";

        [Required]
        public string token { get; set; } = string.Empty;

        [Required]
        public string invited_by_email { get; set; } = string.Empty;

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        public DateTime expires_at { get; set; } = DateTime.UtcNow.AddDays(7);

        public DateTime? accepted_at { get; set; }
    }
}
