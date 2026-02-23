using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class AuditLogsPageViewModel
    {
        public List<AuditLogEntry> Logs { get; set; } = new();
        public string? Search { get; set; }
        public string? Action { get; set; }
    }
}
