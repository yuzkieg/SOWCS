using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT15_SOWCS.Models
{
    public class Projects
    {
        [Key]
        public int project_id { get; set; } // Primary Key

        [Required]
        [StringLength(100)]
        public string name { get; set; }

        public string? description { get; set; }

        [Required]
        [EmailAddress]
        public string manager_email { get; set; } // Foreign Key reference

        public string manager_name { get; set; }

        [Required]
        public string status { get; set; }

        [Required]
        public string priority { get; set; }

        [DataType(DataType.Date)]
        public DateTime start_date { get; set; }

        [DataType(DataType.Date)]
        public DateTime due_date { get; set; }

        // Storing as a string or a navigation property depending on your setup
        public string team_members { get; set; }

        public int progress { get; set; } // Usually represented as 0-100
    }
}