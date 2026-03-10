using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.Models
{
    public class PredictionAction
    {
        [Key]
        public int action_id { get; set; }

        public int employee_id { get; set; }

        [Required]
        public string employee_name { get; set; } = string.Empty;

        [Required]
        public string prediction_label { get; set; } = string.Empty;

        [Required]
        public string action_type { get; set; } = string.Empty;

        public string? action_notes { get; set; }

        public string? created_by { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [Required]
        public string period_type { get; set; } = "month";

        public DateTime period_start { get; set; }

        public DateTime period_end { get; set; }
    }
}
