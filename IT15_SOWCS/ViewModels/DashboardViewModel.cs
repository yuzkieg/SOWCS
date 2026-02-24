namespace IT15_SOWCS.ViewModels
{
    public class DashboardViewModel
    {
        public string FullName { get; set; } = "User";
        public int ActiveProjectsCount { get; set; }
        public int MyTasksCount { get; set; }
        public int CompletedTasksCount { get; set; }
        public int DocumentsCount { get; set; }
        public int PendingLeavesCount { get; set; }
        public int OverallProgressPercent { get; set; }
        public List<DashboardTaskItemViewModel> MyTasks { get; set; } = new();
        public List<DashboardActivityItemViewModel> RecentActivities { get; set; } = new();
    }

    public class DashboardTaskItemViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
    }

    public class DashboardActivityItemViewModel
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}
