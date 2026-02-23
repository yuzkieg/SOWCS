using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT15_SOWCS.Models
{
    public class LeaveRequest
    {
        [Key]
        public int LR_id { get; set; }

        [Required]
        [EmailAddress]
        public string employee_email { get; set; } = string.Empty;

        [Required]
        public string employee_name { get; set; } = string.Empty;

        [Required]
        public string leave_type { get; set; } = string.Empty;

        public DateTime start_date { get; set; }

        public DateTime end_date { get; set; }

        public int days_count { get; set; }

        [Required]
        public string reason { get; set; } = string.Empty;

        [Required]
        public string status { get; set; } = string.Empty;

        public string? reviewed_by { get; set; }

        public DateTime? reviewed_date { get; set; }

        public string? review_notes { get; set; }

        [ForeignKey(nameof(employee_email))]
        public Users? EmployeeUser { get; set; }
    }
}
