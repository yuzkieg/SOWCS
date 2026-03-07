using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class LeaveRequestsPageViewModel
    {
        public List<LeaveRequest> Requests { get; set; } = new();
        public string? Status { get; set; }
        public decimal AnnualLeaveBalance { get; set; }
        public decimal SickLeaveBalance { get; set; }
        public decimal PersonalLeaveBalance { get; set; }
    }
}
