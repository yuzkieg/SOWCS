using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class UserManagementPageViewModel
    {
        public List<Users> Users { get; set; } = new();
        public int TotalUsersCount { get; set; }
        public Dictionary<string, string> EmployeeRolesByEmail { get; set; } = new();
        public Dictionary<string, string> EmployeeNamesByEmail { get; set; } = new();
        public HashSet<string> ActiveEmployeeUserIds { get; set; } = new();
        public int AdminCount { get; set; }
        public int ActiveEmployeeCount { get; set; }
        public string? Search { get; set; }
        public string? SelectedFilter { get; set; }
    }
}
