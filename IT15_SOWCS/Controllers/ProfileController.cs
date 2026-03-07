using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace IT15_SOWCS.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly EmailService _emailService;
        private readonly IMemoryCache _memoryCache;

        public ProfileController(UserManager<Users> userManager, EmailService emailService, IMemoryCache memoryCache)
        {
            _userManager = userManager;
            _emailService = emailService;
            _memoryCache = memoryCache;
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new ProfilePageViewModel
            {
                User = user,
                HasPassword = await _userManager.HasPasswordAsync(user),
                OpenSetPasswordModal = false,
                ResetToken = null
            };

            var verifiedToken = _memoryCache.Get<string>(GetPasswordVerifiedCacheKey(user.Id));
            if (!string.IsNullOrWhiteSpace(verifiedToken))
            {
                model.OpenSetPasswordModal = true;
                model.ResetToken = verifiedToken;
            }
            else if (string.Equals(TempData["OpenSetPasswordModal"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                model.OpenSetPasswordModal = true;
                model.ResetToken = TempData["ProfileResetToken"]?.ToString();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> PasswordVerificationStatus()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { verified = false });
            }

            var token = _memoryCache.Get<string>(GetPasswordVerifiedCacheKey(user.Id));
            return Json(new
            {
                verified = !string.IsNullOrWhiteSpace(token),
                token
            });
        }

        [HttpPost]
        public async Task<IActionResult> ClearPasswordVerificationState()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { cleared = false });
            }

            _memoryCache.Remove(GetPasswordVerifiedCacheKey(user.Id));
            return Json(new { cleared = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetInitialPassword(string newPassword, string confirmPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (await _userManager.HasPasswordAsync(user))
            {
                TempData["ProfileError"] = "Password is already configured.";
                return RedirectToAction(nameof(Profile));
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
            {
                TempData["ProfileError"] = "Passwords do not match.";
                return RedirectToAction(nameof(Profile));
            }

            var result = await _userManager.AddPasswordAsync(user, newPassword);
            if (!result.Succeeded)
            {
                TempData["ProfileError"] = string.Join(" ", result.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Profile));
            }

            TempData["SuccessMessage"] = "Password saved successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPasswordVerificationEmail()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!await _userManager.HasPasswordAsync(user))
            {
                TempData["ProfileError"] = "No existing password found. Set your password first.";
                return RedirectToAction(nameof(Profile));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var verifyLink = Url.Action("VerifyPasswordEmailLink", "Profile", new
            {
                userId = user.Id,
                token
            }, Request.Scheme) ?? string.Empty;
            var displayName = string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? "User") : user.FullName;
            var emailSent = await _emailService.SendResetPasswordEmailAsync(user.Email ?? string.Empty, displayName, verifyLink);

            if (emailSent)
            {
                TempData["SuccessMessage"] = "Verification email sent.";
            }
            else
            {
                TempData["ProfileError"] = "Email settings are not configured. Please configure EmailSettings.";
            }

            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public async Task<IActionResult> VerifyPasswordEmailLink(string userId, string token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                return View("PasswordVerificationFailed");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View("PasswordVerificationFailed");
            }

            var isValid = await _userManager.VerifyUserTokenAsync(
                user,
                _userManager.Options.Tokens.PasswordResetTokenProvider,
                "ResetPassword",
                token);

            if (!isValid)
            {
                return View("PasswordVerificationFailed");
            }

            _memoryCache.Set(GetPasswordVerifiedCacheKey(user.Id), token, TimeSpan.FromMinutes(20));
            return View("PasswordVerificationSuccess");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordFromProfile(string token, string newPassword, string confirmPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["ProfileError"] = "Invalid reset token.";
                return RedirectToAction(nameof(Profile));
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
            {
                TempData["ProfileError"] = "Passwords do not match.";
                TempData["OpenSetPasswordModal"] = "true";
                TempData["ProfileResetToken"] = token;
                return RedirectToAction(nameof(Profile));
            }

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
            {
                TempData["ProfileError"] = string.Join(" ", result.Errors.Select(error => error.Description));
                TempData["OpenSetPasswordModal"] = "true";
                TempData["ProfileResetToken"] = token;
                return RedirectToAction(nameof(Profile));
            }

            _memoryCache.Remove(GetPasswordVerifiedCacheKey(user.Id));
            TempData["SuccessMessage"] = "Password updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        private static string GetPasswordVerifiedCacheKey(string userId) => $"profile-password-verified:{userId}";

    }
}
