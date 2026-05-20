using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.ViewModels
{
    public class MfaSetupViewModel
    {
        public bool IsEnabled { get; set; }
        public bool IsRequired { get; set; }
        public string SharedKey { get; set; } = string.Empty;
        public string AuthenticatorUri { get; set; } = string.Empty;
        public int RecoveryCodesLeft { get; set; }
        public IEnumerable<string> RecoveryCodes { get; set; } = Array.Empty<string>();

        [Required(ErrorMessage = "Authenticator code is required.")]
        [Display(Name = "Authenticator code")]
        public string VerificationCode { get; set; } = string.Empty;
    }
}
