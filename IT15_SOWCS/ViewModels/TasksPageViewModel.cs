using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class TasksPageViewModel
    {
        public List<WorkTask> Tasks { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public List<Projects> Projects { get; set; } = new();
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
    }
}
