using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT15_SOWCS.Models
{
    public class Employee
    {
        [Key]
        public int employee_id { get; set; }

        [Required]
        public string user_id { get; set; } = string.Empty;

        [Required]
        public string full_name { get; set; } = string.Empty;

        [Required]
        public string department { get; set; } = string.Empty;

        [Required]
        public string position { get; set; } = string.Empty;

        [Required]
        public string contact_number { get; set; } = string.Empty;

        public DateTime hire_date { get; set; }

        [EmailAddress]
        public string? manager_email { get; set; }

        public decimal annual_leave_balance { get; set; }

        public decimal sick_leave_balance { get; set; }

        public decimal personal_leave_balance { get; set; }

        [Required]
        public string employee_role { get; set; } = string.Empty;

        public bool is_active { get; set; } = true;

        [ForeignKey(nameof(user_id))]
        public Users? User { get; set; }

        [ForeignKey(nameof(manager_email))]
        public Users? ManagerUser { get; set; }

        public ICollection<WorkTask> Tasks { get; set; } = new List<WorkTask>();
    }
}
