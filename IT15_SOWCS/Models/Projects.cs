using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT15_SOWCS.Models
{
    public class Projects
    {
        [Key]
        public int project_id { get; set; }

        [Required]
        [StringLength(100)]
        public string name { get; set; } = string.Empty;

        public string? description { get; set; }

        [Required]
        [EmailAddress]
        public string manager_email { get; set; } = string.Empty;

        public string manager_name { get; set; } = string.Empty;

        [Required]
        public string status { get; set; } = string.Empty;

        [Required]
        public string priority { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime start_date { get; set; }

        [DataType(DataType.Date)]
        public DateTime due_date { get; set; }

        public string team_members { get; set; } = string.Empty;

        public int progress { get; set; }

        [ForeignKey(nameof(manager_email))]
        public Users? ManagerUser { get; set; }

        public ICollection<WorkTask> Tasks { get; set; } = new List<WorkTask>();
    }
}
