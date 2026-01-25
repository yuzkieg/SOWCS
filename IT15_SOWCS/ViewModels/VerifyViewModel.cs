using System.ComponentModel.DataAnnotations;

namespace IT15_SOWCS.ViewModels
{
    public class VerifyViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; }
    }
}
