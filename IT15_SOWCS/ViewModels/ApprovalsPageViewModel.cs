using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class ApprovalsPageViewModel
    {
        public List<LeaveRequest> PendingLeaveRequests { get; set; } = new();
        public List<DocumentRecord> PendingDocuments { get; set; } = new();
        public bool ShowLeaveApprovals { get; set; } = true;
        public bool ShowDocumentApprovals { get; set; } = true;
        public string ActiveTab { get; set; } = "leave";
    }
}
