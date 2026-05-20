using IT15_SOWCS.Models;
using IT15_SOWCS.Data;
using IT15_SOWCS.Services;
using IT15_SOWCS.Validation;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class ProfileController : Controller
    {
        private const string ProfileErrorKey = "ProfileError";
        private const string PasswordVerificationFailedView = "PasswordVerificationFailed";
        private const string InitialPasswordErrorsKey = "InitialPasswordErrors";
        private const string InitialConfirmPasswordErrorsKey = "InitialConfirmPasswordErrors";
        private const string ResetPasswordErrorsKey = "ResetPasswordErrors";
        private const string ResetConfirmPasswordErrorsKey = "ResetConfirmPasswordErrors";
        private const string OpenSetPasswordModalKey = "OpenSetPasswordModal";
        private const string ProfileResetTokenKey = "ProfileResetToken";
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly IMemoryCache _memoryCache;

        public ProfileController(UserManager<Users> userManager, AppDbContext context, EmailService emailService, IMemoryCache memoryCache)
        {
            _userManager = userManager;
            _context = context;
            _emailService = emailService;
            _memoryCache = memoryCache;
        }

        [HttpGet]
        public async Task<IActionResult> Profile(bool passwordExpired = false, bool requireMfaSetup = false)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (passwordExpired)
            {
                TempData[ProfileErrorKey] = $"Your password expired after {PasswordPolicyService.PasswordExpiryMonths} months. Change it to continue using the system.";
            }

            var isMfaRequired = await MfaPolicyService.IsMfaRequiredAsync(user, _context);
            if (requireMfaSetup && isMfaRequired && !user.TwoFactorEnabled)
            {
                TempData[ProfileErrorKey] = MfaPolicyService.GetRequiredMessage();
            }

            var model = new ProfilePageViewModel
            {
                User = user,
                Employee = await _context.Employees.FirstOrDefaultAsync(employee => employee.user_id == user.Id),
                HasPassword = await _userManager.HasPasswordAsync(user),
                OpenSetPasswordModal = false,
                ResetToken = null,
                IsMfaRequired = isMfaRequired
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
                model.ResetToken = TempData[ProfileResetTokenKey]?.ToString();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MfaSettings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await BuildMfaSetupViewModelAsync(user);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableMfa(MfaSetupViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildMfaSetupViewModelAsync(user);
                invalidModel.VerificationCode = model.VerificationCode;
                return View("MfaSettings", invalidModel);
            }

            var verificationCode = NormalizeAuthenticatorCode(model.VerificationCode);
            var isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                verificationCode);

            if (!isValid)
            {
                ModelState.AddModelError(nameof(MfaSetupViewModel.VerificationCode), "Invalid authenticator code.");
                var invalidModel = await BuildMfaSetupViewModelAsync(user);
                return View("MfaSettings", invalidModel);
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            TempData["SuccessMessage"] = "Multi-factor authentication is now enabled.";
            await AddAuditLogAsync(user, "mfa_enabled", "Enabled authenticator app MFA", "Informational");

            var successModel = await BuildMfaSetupViewModelAsync(user, recoveryCodes);
            return View("MfaSettings", successModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableMfa()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var isRequired = await MfaPolicyService.IsMfaRequiredAsync(user, _context);
            if (isRequired)
            {
                TempData[ProfileErrorKey] = "MFA cannot be disabled while your role requires it.";
                return RedirectToAction(nameof(MfaSettings));
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);
            TempData["SuccessMessage"] = "Multi-factor authentication has been disabled.";
            await AddAuditLogAsync(user, "mfa_disabled", "Disabled authenticator app MFA", "Warning");
            return RedirectToAction(nameof(MfaSettings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateRecoveryCodes()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!user.TwoFactorEnabled)
            {
                TempData[ProfileErrorKey] = "Enable MFA before generating recovery codes.";
                return RedirectToAction(nameof(MfaSettings));
            }

            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            TempData["SuccessMessage"] = "Recovery codes regenerated.";
            await AddAuditLogAsync(user, "mfa_recovery_codes_regenerated", "Regenerated MFA recovery codes", "Informational");

            var model = await BuildMfaSetupViewModelAsync(user, recoveryCodes);
            return View("MfaSettings", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetAuthenticator()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);
            TempData["SuccessMessage"] = "Authenticator key reset. Set up MFA again with your app.";
            await AddAuditLogAsync(user, "mfa_authenticator_reset", "Reset authenticator app key", "Warning");
            return RedirectToAction(nameof(MfaSettings));
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
            if (!ModelState.IsValid)
            {
                TempData[ProfileErrorKey] = "Unable to process the password setup request.";
                return RedirectToAction(nameof(Profile));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (await _userManager.HasPasswordAsync(user))
            {
                TempData[ProfileErrorKey] = "Password is already configured.";
                return RedirectToAction(nameof(Profile));
            }

            var initialPasswordErrors = GetPasswordRequirementErrors(newPassword);
            var initialConfirmPasswordErrors = GetConfirmPasswordErrors(newPassword, confirmPassword);
            if (initialPasswordErrors.Count > 0 || initialConfirmPasswordErrors.Count > 0)
            {
                SetTempDataList(InitialPasswordErrorsKey, initialPasswordErrors);
                SetTempDataList(InitialConfirmPasswordErrorsKey, initialConfirmPasswordErrors);
                return RedirectToAction(nameof(Profile));
            }

            var result = await _userManager.AddPasswordAsync(user, newPassword);
            if (!result.Succeeded)
            {
                SetTempDataList(InitialPasswordErrorsKey, result.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Profile));
            }

            PasswordPolicyService.StampPasswordChanged(user);
            await _userManager.UpdateAsync(user);
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
                TempData[ProfileErrorKey] = "No existing password found. Set your password first.";
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
                TempData[ProfileErrorKey] = "Email settings are not configured. Please configure EmailSettings.";
            }

            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public async Task<IActionResult> VerifyPasswordEmailLink(string userId, string token)
        {
            if (!ModelState.IsValid)
            {
                return View(PasswordVerificationFailedView);
            }

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                return View(PasswordVerificationFailedView);
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View(PasswordVerificationFailedView);
            }

            var isValid = await _userManager.VerifyUserTokenAsync(
                user,
                _userManager.Options.Tokens.PasswordResetTokenProvider,
                "ResetPassword",
                token);

            if (!isValid)
            {
                return View(PasswordVerificationFailedView);
            }

            _memoryCache.Set(GetPasswordVerifiedCacheKey(user.Id), token, TimeSpan.FromMinutes(20));
            return View("PasswordVerificationSuccess");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordFromProfile(string token, string newPassword, string confirmPassword)
        {
            if (!ModelState.IsValid)
            {
                TempData[ProfileErrorKey] = "Unable to process the password reset request.";
                return RedirectToAction(nameof(Profile));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData[ProfileErrorKey] = "Invalid reset token.";
                return RedirectToAction(nameof(Profile));
            }

            var resetPasswordErrors = GetPasswordRequirementErrors(newPassword);
            var resetConfirmPasswordErrors = GetConfirmPasswordErrors(newPassword, confirmPassword);
            if (resetPasswordErrors.Count > 0 || resetConfirmPasswordErrors.Count > 0)
            {
                SetTempDataList(ResetPasswordErrorsKey, resetPasswordErrors);
                SetTempDataList(ResetConfirmPasswordErrorsKey, resetConfirmPasswordErrors);
                TempData[OpenSetPasswordModalKey] = "true";
                TempData[ProfileResetTokenKey] = token;
                return RedirectToAction(nameof(Profile));
            }

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
            {
                SetTempDataList(ResetPasswordErrorsKey, result.Errors.Select(error => error.Description));
                TempData[OpenSetPasswordModalKey] = "true";
                TempData[ProfileResetTokenKey] = token;
                return RedirectToAction(nameof(Profile));
            }

            PasswordPolicyService.StampPasswordChanged(user);
            await _userManager.UpdateAsync(user);
            _memoryCache.Remove(GetPasswordVerifiedCacheKey(user.Id));
            TempData["SuccessMessage"] = "Password updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        private static string GetPasswordVerifiedCacheKey(string userId) => $"profile-password-verified:{userId}";
        
        private static List<string> GetPasswordRequirementErrors(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return new List<string> { "Password is required." };
            }

            return PasswordComplexityAttribute.GetUnmetRequirements(password)
                .Select(requirement => $"Password must contain {requirement}.")
                .ToList();
        }

        private static List<string> GetConfirmPasswordErrors(string? newPassword, string? confirmPassword)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                errors.Add("Confirm Password is required.");
            }
            else if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                errors.Add("The password and confirmation password do not match.");
            }

            return errors;
        }

        private void SetTempDataList(string key, IEnumerable<string> values)
        {
            var items = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (items.Count == 0)
            {
                TempData.Remove(key);
                return;
            }

            TempData[key] = JsonSerializer.Serialize(items);
        }

        private async Task<MfaSetupViewModel> BuildMfaSetupViewModelAsync(Users user, IEnumerable<string>? recoveryCodes = null)
        {
            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            var email = user.Email ?? user.UserName ?? "user";
            var issuer = "Syncora";
            return new MfaSetupViewModel
            {
                IsEnabled = user.TwoFactorEnabled,
                IsRequired = await MfaPolicyService.IsMfaRequiredAsync(user, _context),
                SharedKey = FormatKey(unformattedKey ?? string.Empty),
                AuthenticatorUri = BuildAuthenticatorUri(issuer, email, unformattedKey ?? string.Empty),
                RecoveryCodesLeft = user.TwoFactorEnabled ? await _userManager.CountRecoveryCodesAsync(user) : 0,
                RecoveryCodes = recoveryCodes ?? Array.Empty<string>()
            };
        }

        private async Task AddAuditLogAsync(Users user, string action, string description, string severity)
        {
            _context.AuditLogs.Add(new AuditLogEntry
            {
                timestamp = DateTime.UtcNow,
                user_email = user.Email ?? string.Empty,
                user_name = string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? string.Empty) : user.FullName,
                action = action,
                entity = "Account",
                audit_type = "Security",
                severity = severity,
                ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                description = description
            });
            await _context.SaveChangesAsync();
        }

        private static string NormalizeAuthenticatorCode(string code)
        {
            return (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
        }

        private static string BuildAuthenticatorUri(string issuer, string email, string unformattedKey)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
                Uri.EscapeDataString(issuer),
                Uri.EscapeDataString(email),
                Uri.EscapeDataString(unformattedKey));
        }

        private static string FormatKey(string unformattedKey)
        {
            var result = new System.Text.StringBuilder();
            int currentPosition = 0;

            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }

            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

    }
}
