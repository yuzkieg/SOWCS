using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.ViewModels
{
    public class MfaVerificationViewModel
    {
        [Display(Name = "Authenticator code")]
        public string? Code { get; set; }

        [Display(Name = "Recovery code")]
        public string? RecoveryCode { get; set; }

        public bool UseRecoveryCode { get; set; }

        [Display(Name = "Remember this device for 30 days")]
        public bool RememberMachine { get; set; }

        public string MaskedEmail { get; set; } = string.Empty;
    }
}
