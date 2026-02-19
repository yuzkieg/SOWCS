using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using IT15_SOWCS.Models;
using System.Threading.Tasks;

namespace IT15_SOWCS.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<Users> _userManager;

        public ProfileController(UserManager<Users> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            return View(user);
        }
    }
}