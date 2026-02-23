using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class ApprovalsPageViewModel
    {
        public List<LeaveRequest> PendingLeaveRequests { get; set; } = new();
        public List<DocumentRecord> PendingDocuments { get; set; } = new();
    }
}
