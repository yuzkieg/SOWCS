using Microsoft.AspNetCore.Identity;

namespace IT15_SOWCS.Models
{
    public class Users : IdentityUser
    {
        public string FullName { get; set; }
    }
}
