using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class ProfilePageViewModel
    {
        public Users User { get; set; } = new();
        public Employee? Employee { get; set; }
        public bool HasPassword { get; set; }
        public bool OpenSetPasswordModal { get; set; }
        public string? ResetToken { get; set; }
    }
}
