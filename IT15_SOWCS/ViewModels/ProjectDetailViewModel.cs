using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class ProjectDetailViewModel
    {
        public Projects Project { get; set; } = new();
        public List<Employee> TeamMembers { get; set; } = new();
        public List<WorkTask> Tasks { get; set; } = new();
    }
}

