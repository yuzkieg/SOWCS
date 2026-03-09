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
        public decimal AnnualLeaveBalance { get; set; }
        public decimal SickLeaveBalance { get; set; }
        public decimal PersonalLeaveBalance { get; set; }
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

    public class HrManagerDashboardViewModel
    {
        public string FullName { get; set; } = "HR Manager";
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int PendingLeaves { get; set; }
        public int ApprovedLeaves { get; set; }
        public List<HrPendingLeaveItemViewModel> PendingLeaveRequests { get; set; } = new();
        public List<HrDepartmentLoadItemViewModel> DepartmentLoads { get; set; } = new();
        public List<HrRecentEmployeeItemViewModel> RecentEmployees { get; set; } = new();
    }

    public class HrPendingLeaveItemViewModel
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string LeaveType { get; set; } = string.Empty;
        public int Days { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Pending";
    }

    public class HrDepartmentLoadItemViewModel
    {
        public string Department { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Percent { get; set; }
    }

    public class HrRecentEmployeeItemViewModel
    {
        public string Initials { get; set; } = "U";
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class ProjectManagerDashboardViewModel
    {
        public string FullName { get; set; } = "Project Manager";
        public int ProjectsCount { get; set; }
        public int TeamMembersCount { get; set; }
        public int PendingDocumentsCount { get; set; }
        public int TotalTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int ApprovalsPendingCount { get; set; }
        public List<ProjectManagerProjectProgressItemViewModel> ProjectProgress { get; set; } = new();
        public List<ProjectManagerTaskBreakdownItemViewModel> TaskBreakdownItems { get; set; } = new();
        public List<ProjectManagerTeamMemberItemViewModel> TeamMembers { get; set; } = new();
        public List<ProjectManagerPendingDocumentItemViewModel> PendingDocuments { get; set; } = new();
        public List<ProjectManagerProjectItemViewModel> MyProjects { get; set; } = new();
    }

    public class ProjectManagerProjectProgressItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int ProgressPercent { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ProjectManagerTaskBreakdownItemViewModel
    {
        public string TaskTitle { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Status { get; set; } = "To Do";
    }

    public class ProjectManagerTeamMemberItemViewModel
    {
        public string Initials { get; set; } = "U";
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class ProjectManagerPendingDocumentItemViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public string Status { get; set; } = "Pending";
    }

    public class ProjectManagerProjectItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
    }
}
