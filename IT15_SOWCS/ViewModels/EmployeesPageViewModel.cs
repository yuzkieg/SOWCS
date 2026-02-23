using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class EmployeesPageViewModel
    {
        public List<Employee> Employees { get; set; } = new();
        public List<Users> Users { get; set; } = new();
        public string? Search { get; set; }
        public string? Department { get; set; }
    }
}
