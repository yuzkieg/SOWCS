using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.Models
{
    public class DocumentRecord
    {
        [Key]
        public int document_id { get; set; }

        [Required]
        public string title { get; set; } = string.Empty;

        [Required]
        public string file_name { get; set; } = string.Empty;

        [Required]
        public string file_path { get; set; } = string.Empty;

        [Required]
        public string category { get; set; } = "Other";

        [Required]
        public string status { get; set; } = "Pending";

        public long file_size_bytes { get; set; }

        public DateTime uploaded_date { get; set; } = DateTime.UtcNow;

        [EmailAddress]
        public string? uploaded_by_email { get; set; }

        public string? review_notes { get; set; }

        [EmailAddress]
        public string? reviewed_by { get; set; }

        public DateTime? reviewed_date { get; set; }
    }
}
