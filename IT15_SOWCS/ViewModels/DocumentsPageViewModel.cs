using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class DocumentsPageViewModel
    {
        public List<DocumentRecord> Documents { get; set; } = new();
        public string? Search { get; set; }
        public string? Category { get; set; }
    }
}
