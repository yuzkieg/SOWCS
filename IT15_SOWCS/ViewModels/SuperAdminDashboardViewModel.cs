namespace IT15_SOWCS.ViewModels
{
    public class SuperAdminDashboardViewModel
    {
        public string FullName { get; set; } = "Super Admin";
        public int TasksCompleted { get; set; }
        public int EmployeesCount { get; set; }

        public double TaskVelocityDays { get; set; }
        public int Throughput { get; set; }
        public int ThroughputDeltaPercent { get; set; }
        public int CollaborationScore { get; set; }
        public int CompletionRatePercent { get; set; }
        public int CompletionNumerator { get; set; }
        public int CompletionDenominator { get; set; }

        public List<SuperAdminEmployeeModel> RejectRisks { get; set; } = new();
        public List<SuperAdminEmployeeModel> TopPerformers { get; set; } = new();
        public List<SuperAdminSuggestionModel> Suggestions { get; set; } = new();
        public List<SuperAdminEmployeeAnalyticsRow> AnalyticsRows { get; set; } = new();

        public int[,] Heatmap { get; set; } = new int[5, 9];
        public List<CorrelationPoint> CorrelationPoints { get; set; } = new();
        public List<int> PredictiveActual { get; set; } = new();
        public List<int> PredictiveProjected { get; set; } = new();
    }

    public class SuperAdminEmployeeModel
    {
        public string Initials { get; set; } = "U";
        public string Name { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public int RejectRatePercent { get; set; }
        public int CompletionPercent { get; set; }
        public int WoWPercent { get; set; }
        public int CollaborationScore { get; set; }
        public decimal Velocity { get; set; }
        public string Classification { get; set; } = string.Empty;
    }

    public class SuperAdminSuggestionModel
    {
        public string Type { get; set; } = "risk";
        public string Message { get; set; } = string.Empty;
        public string ActionLabel { get; set; } = string.Empty;
    }

    public class SuperAdminEmployeeAnalyticsRow
    {
        public string Initials { get; set; } = "U";
        public string Name { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public int Tasks { get; set; }
        public int CompletionPercent { get; set; }
        public int RejectPercent { get; set; }
        public int WoWPercent { get; set; }
        public int CollaborationScore { get; set; }
        public string Classification { get; set; } = string.Empty;
    }

    public class CorrelationPoint
    {
        public int Tasks { get; set; }
        public int Reject { get; set; }
    }
}
