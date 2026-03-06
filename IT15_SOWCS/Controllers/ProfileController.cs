using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;

namespace IT15_SOWCS.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IConfiguration _configuration;

        public ProfileController(UserManager<Users> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
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
                OpenSetPasswordModal = string.Equals(TempData["OpenSetPasswordModal"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase),
                ResetToken = TempData["ProfileResetToken"]?.ToString()
            };

            return View(model);
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
            var profileLink = Url.Action("Profile", "Profile", null, Request.Scheme) ?? string.Empty;
            var emailSent = await TrySendPasswordVerificationEmailAsync(user.Email ?? string.Empty, profileLink);

            if (emailSent)
            {
                TempData["SuccessMessage"] = "Verification email sent.";
            }
            else
            {
                TempData["ProfileError"] = "Email settings are not configured. You can set your password now.";
            }

            TempData["OpenSetPasswordModal"] = "true";
            TempData["ProfileResetToken"] = token;
            return RedirectToAction(nameof(Profile));
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

            TempData["SuccessMessage"] = "Password updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        private async Task<bool> TrySendPasswordVerificationEmailAsync(string toEmail, string profileLink)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return false;
            }

            var smtpHost = _configuration["EmailSettings:SmtpHost"];
            var smtpPortRaw = _configuration["EmailSettings:SmtpPort"];
            var smtpUser = _configuration["EmailSettings:Username"];
            var smtpPassword = _configuration["EmailSettings:Password"];
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? smtpUser;
            var enableSsl = bool.TryParse(_configuration["EmailSettings:EnableSsl"], out var parsedSsl) ? parsedSsl : true;

            if (string.IsNullOrWhiteSpace(smtpHost) ||
                string.IsNullOrWhiteSpace(smtpPortRaw) ||
                string.IsNullOrWhiteSpace(smtpUser) ||
                string.IsNullOrWhiteSpace(smtpPassword) ||
                string.IsNullOrWhiteSpace(fromEmail) ||
                !int.TryParse(smtpPortRaw, out var smtpPort))
            {
                return false;
            }

            using var message = new MailMessage(fromEmail, toEmail)
            {
                Subject = "Password Change Verification",
                Body = $"You requested to change your password. Continue from Profile Settings: {profileLink}",
                IsBodyHtml = false
            };

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPassword),
                EnableSsl = enableSsl
            };

            await client.SendMailAsync(message);
            return true;
        }
    }
}
