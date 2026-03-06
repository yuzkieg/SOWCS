namespace IT15_SOWCS.ViewModels
{
    public class ReportsPageViewModel
    {
        public string ActiveTab { get; set; } = "projects";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int RecordsMatch { get; set; }

        public int TotalProjects { get; set; }
        public int ProjectsInProgress { get; set; }
        public int ProjectsCompleted { get; set; }
        public int ProjectsOnHold { get; set; }

        public int TotalTasks { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksInProgress { get; set; }
        public int TasksPendingReview { get; set; }

        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int TotalDepartments { get; set; }
        public int TotalManagers { get; set; }

        public int TotalLeaveRequests { get; set; }
        public int PendingLeaveRequests { get; set; }
        public int ApprovedLeaveRequests { get; set; }
        public int RejectedLeaveRequests { get; set; }

        public List<ProjectReportRow> ProjectRows { get; set; } = new();
        public List<TaskReportRow> TaskRows { get; set; } = new();
        public List<EmployeeReportRow> EmployeeRows { get; set; } = new();
        public List<LeaveReportRow> LeaveRows { get; set; } = new();
    }

    public class ProjectReportRow
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int Progress { get; set; }
    }

    public class TaskReportRow
    {
        public string Title { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
    }

    public class EmployeeReportRow
    {
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class LeaveReportRow
    {
        public string Employee { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Days { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ReviewedBy { get; set; } = string.Empty;
    }
}
