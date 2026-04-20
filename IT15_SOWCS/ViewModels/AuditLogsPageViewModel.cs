using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class AuditLogsPageViewModel
    {
        public List<AuditLogEntry> Logs { get; set; } = new();
        public string AuditType { get; set; } = "system";
        public string? Search { get; set; }
        public string? Action { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int SystemCount { get; set; }
        public int SecurityCount { get; set; }
        public int RecordsMatch => Logs.Count;
    }
}
