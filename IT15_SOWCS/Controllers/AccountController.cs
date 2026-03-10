using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using IT15_SOWCS.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IT15_SOWCS.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> signInManager;
        private readonly UserManager<Users> userManager;
        private readonly IMemoryCache memoryCache;
        private readonly EmailService emailService;
        private readonly AppDbContext _context;

        public AccountController(
            SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            IMemoryCache memoryCache,
            EmailService emailService,
            AppDbContext context)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.memoryCache = memoryCache;
            this.emailService = emailService;
            _context = context;
        }

        public IActionResult Login(string? inactiveEmail = null)
        {
            if (!string.IsNullOrWhiteSpace(inactiveEmail))
            {
                ViewData["InactiveEmail"] = inactiveEmail.Trim().ToLowerInvariant();
                ViewData["InactiveNotice"] = "Your account has been inactive. Send a reactivation request below.";
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Dashboard");
                }
                if (result.IsLockedOut)
                {
                    ViewData["InactiveEmail"] = model.Email?.Trim();
                    ModelState.AddModelError("", "Your account has been inactive. Send a reactivation request below.");
                    return View(model);
                }

                ModelState.AddModelError("", "Wrong email or password.");
            }

            return View(model);
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new Users
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName
                };

                var result = await userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    return RedirectToAction("Login", "Account");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyEmail(string? email = null)
        {
            return View(new VerifyViewModel
            {
                Email = email ?? string.Empty,
                IsCodeStep = false
            });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyEmail(VerifyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLowerInvariant();
            var user = await userManager.FindByEmailAsync(normalizedEmail);
            if (user == null)
            {
                ModelState.AddModelError("", "Account not found.");
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(model.VerificationCode))
            {
                var cachedCode = memoryCache.Get<string>(GetVerificationCacheKey(normalizedEmail));
                if (string.IsNullOrWhiteSpace(cachedCode) || !string.Equals(cachedCode, model.VerificationCode.Trim(), StringComparison.Ordinal))
                {
                    ModelState.AddModelError("", "Invalid or expired verification code.");
                    model.IsCodeStep = true;
                    return View(model);
                }

                memoryCache.Remove(GetVerificationCacheKey(normalizedEmail));
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                return RedirectToAction("ChangePassword", "Account", new { username = normalizedEmail, token });
            }

            if (!model.IsCodeStep)
            {
                var code = Random.Shared.Next(100000, 999999).ToString();
                memoryCache.Set(GetVerificationCacheKey(normalizedEmail), code, TimeSpan.FromMinutes(10));

                var displayName = string.IsNullOrWhiteSpace(user.FullName)
                    ? normalizedEmail.Split('@')[0]
                    : user.FullName;
                var sent = await emailService.SendVerificationCodeEmailAsync(normalizedEmail, displayName, code);
                if (!sent)
                {
                    ModelState.AddModelError("", "Unable to send verification code email. Please configure EmailSettings.");
                    return View(model);
                }

                TempData["SuccessMessage"] = "Verification code sent to your email.";
                return View(new VerifyViewModel
                {
                    Email = normalizedEmail,
                    IsCodeStep = true
                });
            }

            model.IsCodeStep = true;
            ModelState.AddModelError("", "Enter the verification code sent to your email.");
            return View(model);
        }

        [HttpGet]
        public IActionResult ChangePassword(string username, string token)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction("VerifyEmail", "Account");
            }

            return View(new ChangePasswordViewModel
            {
                Email = username,
                ResetToken = token
            });
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Something went wrong. Try again.");
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLowerInvariant();
            var user = await userManager.FindByEmailAsync(normalizedEmail);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(model);
            }

            var result = await userManager.ResetPasswordAsync(user, model.ResetToken, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                return View(model);
            }

            TempData["SuccessMessage"] = "Password changed successfully.";
            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(returnUrl))
            {
                returnUrl = Url.Action("Index", "Dashboard");
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
            {
                TempData["Error"] = $"Error from external provider: {remoteError}";
                return RedirectToAction(nameof(Login));
            }

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["Error"] = "Error loading external login information.";
                return RedirectToAction(nameof(Login));
            }

            var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);

            if (result.Succeeded)
            {
                await signInManager.UpdateExternalAuthenticationTokensAsync(info);
                var safeRedirectUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? returnUrl
                    : Url.Action("Index", "Dashboard") ?? "/";
                return LocalRedirect(safeRedirectUrl);
            }
            if (result.IsLockedOut)
            {
                var lockedEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (!string.IsNullOrWhiteSpace(lockedEmail))
                {
                    TempData["InactiveEmail"] = lockedEmail.Trim().ToLowerInvariant();
                }
                TempData["Error"] = "Your account has been inactive. Send a reactivation request below.";
                return RedirectToAction(nameof(Login));
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email == null)
            {
                TempData["Error"] = "Email not provided by Google.";
                return RedirectToAction(nameof(Login));
            }

            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                var givenName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
                var familyName = info.Principal.FindFirstValue(ClaimTypes.Surname);
                var fullName = info.Principal.FindFirstValue(ClaimTypes.Name);

                var displayName = !string.IsNullOrEmpty(fullName)
                    ? fullName
                    : $"{givenName ?? ""} {familyName ?? ""}".Trim();

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = email.Split('@')[0];
                }

                user = new Users
                {
                    UserName = email,
                    Email = email,
                    FullName = displayName,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    foreach (var err in createResult.Errors)
                    {
                        ModelState.AddModelError("", err.Description);
                    }
                    return View("Login");
                }

                await userManager.AddLoginAsync(user, info);
            }
            else if (await userManager.IsLockedOutAsync(user))
            {
                TempData["InactiveEmail"] = email.Trim().ToLowerInvariant();
                TempData["Error"] = "Your account has been inactive. Send a reactivation request below.";
                return RedirectToAction(nameof(Login));
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            await signInManager.UpdateExternalAuthenticationTokensAsync(info);
            var finalRedirectUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : Url.Action("Index", "Dashboard") ?? "/";
            return LocalRedirect(finalRedirectUrl);
        }

        [HttpGet]
        public async Task<IActionResult> AcceptInvitation(string token)
        {
            var normalizedToken = (token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                TempData["Error"] = "Invalid invitation link.";
                return RedirectToAction(nameof(Login));
            }

            var invitation = await _context.PendingInvitations
                .FirstOrDefaultAsync(item => item.token == normalizedToken && item.accepted_at == null);
            if (invitation == null || invitation.expires_at < DateTime.UtcNow)
            {
                TempData["Error"] = "Invitation link is invalid or expired.";
                return RedirectToAction(nameof(Login));
            }

            ViewData["InviteToken"] = normalizedToken;
            return View(new RegisterViewModel
            {
                Email = invitation.email,
                FullName = string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptInvitation(string token, RegisterViewModel model)
        {
            var normalizedToken = (token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                ModelState.AddModelError("", "Invalid invitation token.");
                ViewData["InviteToken"] = normalizedToken;
                return View(model);
            }

            var invitation = await _context.PendingInvitations
                .FirstOrDefaultAsync(item => item.token == normalizedToken && item.accepted_at == null);

            if (invitation == null || invitation.expires_at < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "This invitation is invalid or already expired.");
                ViewData["InviteToken"] = normalizedToken;
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                model.Email = invitation.email;
                ViewData["InviteToken"] = normalizedToken;
                return View(model);
            }

            var normalizedEmail = invitation.email.Trim().ToLowerInvariant();
            var existing = await userManager.FindByEmailAsync(normalizedEmail);
            if (existing != null)
            {
                ModelState.AddModelError("", "This invitation has already been used.");
                model.Email = normalizedEmail;
                ViewData["InviteToken"] = normalizedToken;
                return View(model);
            }

            var user = new Users
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                FullName = string.IsNullOrWhiteSpace(model.FullName) ? normalizedEmail.Split('@')[0] : model.FullName.Trim(),
                Role = string.IsNullOrWhiteSpace(invitation.role) ? "user" : invitation.role.Trim().ToLowerInvariant(),
                EmailConfirmed = true,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                model.Email = normalizedEmail;
                ViewData["InviteToken"] = normalizedToken;
                return View(model);
            }

            invitation.accepted_at = DateTime.UtcNow;
            _context.PendingInvitations.Update(invitation);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Invitation accepted. You can now sign in.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestReactivation(string email, string message)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                TempData["Error"] = "Email is required.";
                return RedirectToAction(nameof(Login));
            }

            var superAdminEmail = await userManager.Users
                .Where(user => user.Role != null && user.Role.ToLower() == "superadmin")
                .Select(user => user.Email)
                .FirstOrDefaultAsync();

            var targetEmail = string.IsNullOrWhiteSpace(superAdminEmail) ? "yuzkiega@gmail.com" : superAdminEmail;
            var notes = string.IsNullOrWhiteSpace(message) ? "Please reactivate my account." : message.Trim();

            var sent = await emailService.SendAccountReactivationRequestAsync(targetEmail, normalizedEmail, notes);
            TempData["SuccessMessage"] = sent
                ? "Reactivation request sent successfully."
                : "Unable to send reactivation request email right now.";
            TempData["InactiveEmail"] = normalizedEmail;
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        private static string GetVerificationCacheKey(string email) => $"verify-code:{email}";
    }
}
