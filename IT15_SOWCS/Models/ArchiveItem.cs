using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.Models
{
    public class ArchiveItem
    {
        [Key]
        public int archive_item_id { get; set; }

        public int? source_id { get; set; }

        public string source_type { get; set; } = string.Empty;

        public string title { get; set; } = string.Empty;

        public string type { get; set; } = string.Empty;

        public string archived_by { get; set; } = string.Empty;

        public DateTime date_archived { get; set; } = DateTime.UtcNow;

        public string reason { get; set; } = string.Empty;

        public string? serialized_data { get; set; }

        public bool is_restored { get; set; }
    }
}
