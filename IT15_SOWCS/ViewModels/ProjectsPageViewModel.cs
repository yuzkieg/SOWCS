using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class ProjectsPageViewModel
    {
        public List<Projects> Projects { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public Dictionary<int, int> ProgressByProjectId { get; set; } = new();
        public string? Search { get; set; }
        public string? Status { get; set; }
        public bool CanManageProjects { get; set; }
    }
}
