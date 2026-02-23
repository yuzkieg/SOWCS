using Microsoft.AspNetCore.Identity;

namespace IT15_SOWCS.Models
{
    public class Users : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
        public ICollection<Employee> ManagedEmployees { get; set; } = new List<Employee>();
        public ICollection<Projects> ManagedProjects { get; set; } = new List<Projects>();
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    }
}
