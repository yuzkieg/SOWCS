namespace IT15_SOWCS.ViewModels
{
    public class PredictionInsightsViewModel
    {
        public string Period { get; set; } = "month";
        public string ActiveTab { get; set; } = "all";
        public List<PredictionInsightRow> Rows { get; set; } = new();
    }

    public class PredictionInsightRow
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int TotalApproved { get; set; }
        public int TotalRejected { get; set; }
        public string Prediction { get; set; } = "Stable";
        public string SuggestedAction { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }
}
