namespace IT15_SOWCS.ViewModels
{
    public class ArchiveItemModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; 
        public string ArchivedBy { get; set; } = string.Empty;
        public DateTime DateArchived { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}