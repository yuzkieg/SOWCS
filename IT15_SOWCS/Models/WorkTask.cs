using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT15_SOWCS.Models
{
    public class WorkTask
    {
        [Key]
        public int task_id { get; set; }

        public int employee_id { get; set; }

        public int project_id { get; set; }

        [Required]
        public string title { get; set; } = string.Empty;

        public string? description { get; set; }

        public string? project_name { get; set; }

        public string assigned_to { get; set; } = string.Empty;

        public string assigned_name { get; set; } = string.Empty;

        [Required]
        public string status { get; set; } = string.Empty;

        [Required]
        public string priority { get; set; } = string.Empty;

        public DateTime due_date { get; set; }

        public DateTime? completed_date { get; set; }

        [ForeignKey(nameof(employee_id))]
        public Employee? Employee { get; set; }

        [ForeignKey(nameof(project_id))]
        public Projects? Project { get; set; }
    }
}
