using IT15_SOWCS.Models;

namespace IT15_SOWCS.ViewModels
{
    public class ProfilePageViewModel
    {
        public Users User { get; set; } = new();
        public bool HasPassword { get; set; }
        public bool OpenSetPasswordModal { get; set; }
        public string? ResetToken { get; set; }
    }
}
