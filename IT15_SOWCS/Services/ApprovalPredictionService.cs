using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace IT15_SOWCS.Services
{
    public class ApprovalPredictionService
    {
        private readonly InferenceSession? _session;
        private readonly ILogger<ApprovalPredictionService> _logger;

        public ApprovalPredictionService(IWebHostEnvironment environment, ILogger<ApprovalPredictionService> logger)
        {
            _logger = logger;

            var candidatePaths = new List<string>
            {
                Path.Combine(environment.ContentRootPath, "Trained_Model", "approval_prediction.onnx"),
                Path.Combine(AppContext.BaseDirectory, "Trained_Model", "approval_prediction.onnx")
            };

            if (!string.IsNullOrWhiteSpace(environment.WebRootPath))
            {
                candidatePaths.Add(Path.Combine(environment.WebRootPath, "Trained_Model", "approval_prediction.onnx"));
            }

            candidatePaths.Add(Path.Combine(environment.ContentRootPath, "wwwroot", "Trained_Model", "approval_prediction.onnx"));

            var modelPath = candidatePaths.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                _session = new InferenceSession(modelPath);
                _logger.LogInformation("Approval prediction model loaded from {ModelPath}", modelPath);
            }
            else
            {
                _logger.LogWarning("Approval prediction model not found. Checked paths: {Paths}", string.Join(" | ", candidatePaths));
            }
        }

        public bool IsReady => _session != null;

        public ApprovalPredictionResult Predict(ApprovalPredictionFeatures features)
        {
            if (_session == null)
            {
                return ApprovalPredictionResult.Empty();
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("window_type", new DenseTensor<string>(new[] { features.WindowType }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("leave_total", new DenseTensor<long>(new[] { features.LeaveTotal }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("leave_approved", new DenseTensor<long>(new[] { features.LeaveApproved }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("leave_rejected", new DenseTensor<long>(new[] { features.LeaveRejected }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("doc_total", new DenseTensor<long>(new[] { features.DocTotal }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("doc_approved", new DenseTensor<long>(new[] { features.DocApproved }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("doc_rejected", new DenseTensor<long>(new[] { features.DocRejected }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("total_requests", new DenseTensor<long>(new[] { features.TotalRequests }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("total_approved", new DenseTensor<long>(new[] { features.TotalApproved }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("total_rejected", new DenseTensor<long>(new[] { features.TotalRejected }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("overall_approval_rate", new DenseTensor<double>(new[] { features.OverallApprovalRate }, new[] { 1, 1 })),
                NamedOnnxValue.CreateFromTensor("overall_reject_rate", new DenseTensor<double>(new[] { features.OverallRejectRate }, new[] { 1, 1 }))
            };

            using var results = _session.Run(inputs);
            var label = results.FirstOrDefault(item => item.Name == "output_label")
                            ?.AsEnumerable<string>()
                            ?.FirstOrDefault() ?? "stable";

            var probabilities = results.FirstOrDefault(item => item.Name == "output_probability")
                                ?.AsEnumerable<Dictionary<string, float>>()
                                ?.FirstOrDefault()
                                ?? new Dictionary<string, float>();

            var confidence = probabilities.Count == 0 ? 0 : probabilities.Values.Max();

            return new ApprovalPredictionResult
            {
                Label = label,
                Confidence = confidence,
                Probabilities = probabilities
            };
        }
    }

    public class ApprovalPredictionFeatures
    {
        public string WindowType { get; set; } = "month";
        public long LeaveTotal { get; set; }
        public long LeaveApproved { get; set; }
        public long LeaveRejected { get; set; }
        public long DocTotal { get; set; }
        public long DocApproved { get; set; }
        public long DocRejected { get; set; }
        public long TotalRequests { get; set; }
        public long TotalApproved { get; set; }
        public long TotalRejected { get; set; }
        public double OverallApprovalRate { get; set; }
        public double OverallRejectRate { get; set; }
    }

    public class ApprovalPredictionResult
    {
        public string Label { get; set; } = "stable";
        public double Confidence { get; set; }
        public Dictionary<string, float> Probabilities { get; set; } = new();

        public static ApprovalPredictionResult Empty()
        {
            return new ApprovalPredictionResult
            {
                Label = "stable",
                Confidence = 0,
                Probabilities = new Dictionary<string, float>()
            };
        }
    }
}
