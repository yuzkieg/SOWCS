using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.ViewModels
{
    public class VerifyViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Verification Code")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits.")]
        public string? VerificationCode { get; set; }

        public bool IsCodeStep { get; set; }
    }
}
