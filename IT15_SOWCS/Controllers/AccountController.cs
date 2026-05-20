using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using IT15_SOWCS.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class AccountController : Controller
    {
        private const string ErrorTempDataKey = "Error";
        private const string PasswordExpiredMessage = "Your password has expired. Please change it before using the rest of the system.";
        private const string PendingMfaLoginTempDataKey = "PendingMfaLogin";
        private readonly SignInManager<Users> signInManager;
        private readonly UserManager<Users> userManager;
        private readonly IMemoryCache memoryCache;
        private readonly EmailService emailService;
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public AccountController(
            SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            IMemoryCache memoryCache,
            EmailService emailService,
            AppDbContext context,
            NotificationService notificationService)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.memoryCache = memoryCache;
            this.emailService = emailService;
            _context = context;
            _notificationService = notificationService;
        }

        public IActionResult Login(string? inactiveEmail = null)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

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
                var normalizedEmail = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
                var clientIp = ResolveClientIp(HttpContext);
                var user = await userManager.FindByEmailAsync(normalizedEmail);
                if (user == null)
                {
                    _context.AuditLogs.Add(new AuditLogEntry
                    {
                        timestamp = DateTime.UtcNow,
                        user_email = normalizedEmail,
                        user_name = normalizedEmail,
                        action = "login_failed",
                        entity = "Account",
                        audit_type = "Security",
                        severity = "Warning",
                        ip_address = clientIp,
                        description = "Failed login attempt"
                    });
                    await _context.SaveChangesAsync();
                    ModelState.AddModelError("", "Wrong email or password.");
                    return View(model);
                }

                if (!user.LockoutEnabled)
                {
                    user.LockoutEnabled = true;
                    await userManager.UpdateAsync(user);
                }

                if (!user.LockoutEnd.HasValue && user.AccessFailedCount >= 3)
                {
                    user.AccessFailedCount = 0;
                    await userManager.UpdateAsync(user);
                }

                if (await userManager.IsLockedOutAsync(user))
                {
                    if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow.AddYears(50))
                    {
                        ViewData["InactiveEmail"] = normalizedEmail;
                        return View(model);
                    }

                    var remainingSeconds = user.LockoutEnd.HasValue
                        ? Math.Max(1, (int)Math.Ceiling((user.LockoutEnd.Value - DateTimeOffset.UtcNow).TotalSeconds))
                        : 60;
                    var remainingMinutes = Math.Max(1, (int)Math.Ceiling(remainingSeconds / 60.0));
                    ViewData["LockoutSeconds"] = remainingSeconds;
                    return View(model);
                }

                var passwordValid = await userManager.CheckPasswordAsync(user, model.Password);
                if (!passwordValid)
                {
                    user.AccessFailedCount += 1;
                    var failedCount = user.AccessFailedCount;
                    var failedUpdate = await userManager.UpdateAsync(user);
                    if (!failedUpdate.Succeeded)
                    {
                        ModelState.AddModelError("", "Unable to update login attempts. Please try again.");
                        return View(model);
                    }

                    _context.AuditLogs.Add(new AuditLogEntry
                    {
                        timestamp = DateTime.UtcNow,
                        user_email = normalizedEmail,
                        user_name = string.IsNullOrWhiteSpace(user.FullName) ? normalizedEmail : user.FullName,
                        action = "login_failed",
                        entity = "Account",
                        audit_type = "Security",
                        severity = "Warning",
                        ip_address = clientIp,
                        description = "Failed login attempt"
                    });
                    await _context.SaveChangesAsync();

                    if (failedCount > 0 && failedCount % 3 == 0)
                    {
                        var lockoutStage = failedCount / 3;
                        if (lockoutStage >= 3)
                        {
                            user.LockoutEnabled = true;
                            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));

                            var employeeRecords = await _context.Employees
                                .Where(employee => employee.user_id == user.Id)
                                .ToListAsync();
                            if (employeeRecords.Count > 0)
                            {
                                foreach (var employee in employeeRecords)
                                {
                                    employee.is_active = false;
                                }
                                await _context.SaveChangesAsync();
                            }

                            var notice = $"Account {normalizedEmail} was automatically inactivated after repeated failed login attempts.";
                            await _notificationService.AddForRoleGroupAsync(
                                "superadmin",
                                "Account Auto-Inactivated",
                                notice,
                                "Security",
                                "/UserManagement/UserManagement");
                            await _notificationService.SaveAsync();

                            ViewData["InactiveEmail"] = normalizedEmail;
                            return View(model);
                        }

                        var lockoutMinutes = lockoutStage == 1 ? 1 : 3;

                        user.LockoutEnabled = true;
                        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(lockoutMinutes));
                        ViewData["LockoutSeconds"] = lockoutMinutes * 60;
                        return View(model);
                    }

                    ModelState.AddModelError("", "Wrong email or password.");
                    return View(model);
                }

                await userManager.ResetAccessFailedCountAsync(user);
                await userManager.SetLockoutEndDateAsync(user, null);

                if (await ShouldChallengeForMfaAsync(user))
                {
                    SetPendingMfaLogin(new PendingMfaLoginState
                    {
                        UserId = user.Id,
                        RememberMe = model.RememberMe ?? false,
                        ReturnUrl = Url.Action("Index", "Dashboard"),
                        LoginProvider = "password"
                    });
                    return RedirectToAction(nameof(VerifyMfa));
                }

                await signInManager.SignInAsync(user, model.RememberMe ?? false);
                if (PasswordPolicyService.IsPasswordExpired(user))
                {
                    return RedirectExpiredPasswordUser();
                }

                _context.AuditLogs.Add(new AuditLogEntry
                {
                    timestamp = DateTime.UtcNow,
                    user_email = normalizedEmail,
                    user_name = string.IsNullOrWhiteSpace(user.FullName) ? normalizedEmail : user.FullName,
                    action = "login",
                    entity = "Account",
                    audit_type = "Security",
                    severity = "Informational",
                    ip_address = clientIp,
                    description = "Logged in"
                });
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Dashboard");
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
                PasswordPolicyService.StampPasswordChanged(user);

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
            if (!ModelState.IsValid)
            {
                return View(new VerifyViewModel());
            }

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

            if (!model.IsCodeStep.GetValueOrDefault())
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
            if (!ModelState.IsValid)
            {
                return RedirectToAction("VerifyEmail", "Account");
            }

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

            PasswordPolicyService.StampPasswordChanged(user);
            await userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Password changed successfully.";
            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Login));
            }

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
            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "Unable to complete the external login request.";
                return RedirectToAction(nameof(Login));
            }

            if (remoteError != null)
            {
                TempData[ErrorTempDataKey] = $"Error from external provider: {remoteError}";
                return RedirectToAction(nameof(Login));
            }

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData[ErrorTempDataKey] = "Error loading external login information.";
                return RedirectToAction(nameof(Login));
            }

            var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);

            if (result.Succeeded)
            {
                await signInManager.UpdateExternalAuthenticationTokensAsync(info);
                var signedInUser = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (signedInUser != null && PasswordPolicyService.IsPasswordExpired(signedInUser))
                {
                    return RedirectExpiredPasswordUser();
                }

                if (signedInUser != null && await ShouldChallengeForMfaAsync(signedInUser))
                {
                    await signInManager.SignOutAsync();
                    SetPendingMfaLogin(new PendingMfaLoginState
                    {
                        UserId = signedInUser.Id,
                        RememberMe = false,
                        ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                            ? returnUrl
                            : Url.Action("Index", "Dashboard"),
                        LoginProvider = info.LoginProvider
                    });
                    return RedirectToAction(nameof(VerifyMfa));
                }

                var safeRedirectUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? returnUrl
                    : Url.Action("Index", "Dashboard") ?? "/";
                var loginEmail = info.Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
                var fullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
                var displayName = string.IsNullOrWhiteSpace(fullName) ? loginEmail : fullName;
                await AddAuditLogAsync(loginEmail, displayName, "login", "Signed in with Google", "Informational");
                return LocalRedirect(safeRedirectUrl);
            }
            if (result.IsLockedOut)
            {
                var lockedEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (!string.IsNullOrWhiteSpace(lockedEmail))
                {
                    TempData["InactiveEmail"] = lockedEmail.Trim().ToLowerInvariant();
                }
                TempData[ErrorTempDataKey] = "Your account has been inactive. Send a reactivation request below.";
                return RedirectToAction(nameof(Login));
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email == null)
            {
                TempData[ErrorTempDataKey] = "Email not provided by Google.";
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
                TempData[ErrorTempDataKey] = "Your account has been inactive. Send a reactivation request below.";
                return RedirectToAction(nameof(Login));
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            await signInManager.UpdateExternalAuthenticationTokensAsync(info);
            if (PasswordPolicyService.IsPasswordExpired(user))
            {
                return RedirectExpiredPasswordUser();
            }

            if (await ShouldChallengeForMfaAsync(user))
            {
                await signInManager.SignOutAsync();
                SetPendingMfaLogin(new PendingMfaLoginState
                {
                    UserId = user.Id,
                    RememberMe = false,
                    ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                        ? returnUrl
                        : Url.Action("Index", "Dashboard"),
                    LoginProvider = info.LoginProvider
                });
                return RedirectToAction(nameof(VerifyMfa));
            }

            var finalRedirectUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : Url.Action("Index", "Dashboard") ?? "/";
            await AddAuditLogAsync(user.Email ?? email, string.IsNullOrWhiteSpace(user.FullName) ? email : user.FullName, "login", "Signed in with Google", "Informational");
            return LocalRedirect(finalRedirectUrl);
        }

        [HttpGet]
        public async Task<IActionResult> AcceptInvitation(string token)
        {
            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "Invalid invitation link.";
                return RedirectToAction(nameof(Login));
            }

            var normalizedToken = (token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                TempData[ErrorTempDataKey] = "Invalid invitation link.";
                return RedirectToAction(nameof(Login));
            }

            var invitation = await _context.PendingInvitations
                .FirstOrDefaultAsync(item => item.token == normalizedToken && item.accepted_at == null);
            if (invitation == null || invitation.expires_at < DateTime.UtcNow)
            {
                TempData[ErrorTempDataKey] = "Invitation link is invalid or expired.";
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
            if (!ModelState.IsValid && string.IsNullOrWhiteSpace((token ?? string.Empty).Trim()))
            {
                ModelState.AddModelError("", "Invalid invitation token.");
                ViewData["InviteToken"] = string.Empty;
                return View(model);
            }

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
            PasswordPolicyService.StampPasswordChanged(user);

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
            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "Unable to process the reactivation request.";
                return RedirectToAction(nameof(Login));
            }

            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                TempData[ErrorTempDataKey] = "Email is required.";
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

        [HttpGet]
        public async Task<IActionResult> VerifyMfa()
        {
            var pendingState = GetPendingMfaLogin();
            if (pendingState == null)
            {
                TempData[ErrorTempDataKey] = "Your MFA verification session has expired. Please sign in again.";
                return RedirectToAction(nameof(Login));
            }

            var user = await userManager.FindByIdAsync(pendingState.UserId);
            if (user == null)
            {
                ClearPendingMfaLogin();
                TempData[ErrorTempDataKey] = "Unable to continue MFA verification. Please sign in again.";
                return RedirectToAction(nameof(Login));
            }

            KeepPendingMfaLogin();
            return View(new MfaVerificationViewModel
            {
                MaskedEmail = MaskEmail(user.Email)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyMfa(MfaVerificationViewModel model)
        {
            var pendingState = GetPendingMfaLogin();
            if (pendingState == null)
            {
                TempData[ErrorTempDataKey] = "Your MFA verification session has expired. Please sign in again.";
                return RedirectToAction(nameof(Login));
            }

            var user = await userManager.FindByIdAsync(pendingState.UserId);
            if (user == null)
            {
                ClearPendingMfaLogin();
                TempData[ErrorTempDataKey] = "Unable to continue MFA verification. Please sign in again.";
                return RedirectToAction(nameof(Login));
            }

            var verificationSucceeded = false;
            var usedRecoveryCode = false;

            if (model.UseRecoveryCode)
            {
                var recoveryCode = (model.RecoveryCode ?? string.Empty).Replace(" ", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(recoveryCode))
                {
                    ModelState.AddModelError(nameof(MfaVerificationViewModel.RecoveryCode), "Recovery code is required.");
                }
                else
                {
                    var recoveryResult = await userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode);
                    verificationSucceeded = recoveryResult.Succeeded;
                    usedRecoveryCode = verificationSucceeded;
                }
            }
            else
            {
                var verificationCode = NormalizeAuthenticatorCode(model.Code);
                if (string.IsNullOrWhiteSpace(verificationCode))
                {
                    ModelState.AddModelError(nameof(MfaVerificationViewModel.Code), "Authenticator code is required.");
                }
                else
                {
                    verificationSucceeded = await userManager.VerifyTwoFactorTokenAsync(
                        user,
                        userManager.Options.Tokens.AuthenticatorTokenProvider,
                        verificationCode);
                }
            }

            if (!verificationSucceeded)
            {
                await AddAuditLogAsync(
                    user.Email ?? string.Empty,
                    string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? string.Empty) : user.FullName,
                    "mfa_failed",
                    model.UseRecoveryCode ? "Failed recovery code verification" : "Failed authenticator code verification",
                    "Warning");
                ModelState.AddModelError("", model.UseRecoveryCode ? "Invalid recovery code." : "Invalid authenticator code.");
                KeepPendingMfaLogin();
                model.MaskedEmail = MaskEmail(user.Email);
                return View(model);
            }

            await signInManager.SignInAsync(user, pendingState.RememberMe);
            if (model.RememberMachine && !usedRecoveryCode)
            {
                await signInManager.RememberTwoFactorClientAsync(user);
            }

            ClearPendingMfaLogin();

            if (PasswordPolicyService.IsPasswordExpired(user))
            {
                return RedirectExpiredPasswordUser();
            }

            var displayName = string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? string.Empty) : user.FullName;
            await AddAuditLogAsync(
                user.Email ?? string.Empty,
                displayName,
                "login",
                pendingState.LoginProvider == "password"
                    ? "Logged in with password and MFA"
                    : $"Signed in with {pendingState.LoginProvider} and MFA",
                "Informational");

            if (usedRecoveryCode)
            {
                await AddAuditLogAsync(
                    user.Email ?? string.Empty,
                    displayName,
                    "mfa_recovery_code_used",
                    "Used an MFA recovery code during sign-in",
                    "Warning");
            }

            var redirectUrl = !string.IsNullOrWhiteSpace(pendingState.ReturnUrl) && Url.IsLocalUrl(pendingState.ReturnUrl)
                ? pendingState.ReturnUrl
                : Url.Action("Index", "Dashboard") ?? "/";
            return LocalRedirect(redirectUrl);
        }

        private RedirectToActionResult RedirectExpiredPasswordUser()
        {
            TempData["ProfileError"] = PasswordExpiredMessage;
            return RedirectToAction("Profile", "Profile", new { passwordExpired = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var email = User.Identity?.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(email))
            {
                var dbUser = await userManager.FindByEmailAsync(email);
                var displayName = dbUser != null && !string.IsNullOrWhiteSpace(dbUser.FullName) ? dbUser.FullName : email;
                await AddAuditLogAsync(email, displayName, "logout", "Logged out", "Informational");
            }
            await signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        private static string GetVerificationCacheKey(string email) => $"verify-code:{email}";

        private static string NormalizeAuthenticatorCode(string? code)
        {
            return (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
        }

        private async Task<bool> ShouldChallengeForMfaAsync(Users user)
        {
            return user.TwoFactorEnabled && !await signInManager.IsTwoFactorClientRememberedAsync(user);
        }

        private void SetPendingMfaLogin(PendingMfaLoginState state)
        {
            TempData[PendingMfaLoginTempDataKey] = JsonSerializer.Serialize(state);
        }

        private PendingMfaLoginState? GetPendingMfaLogin()
        {
            var raw = TempData.Peek(PendingMfaLoginTempDataKey)?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<PendingMfaLoginState>(raw);
            }
            catch
            {
                return null;
            }
        }

        private void KeepPendingMfaLogin()
        {
            TempData.Keep(PendingMfaLoginTempDataKey);
        }

        private void ClearPendingMfaLogin()
        {
            TempData.Remove(PendingMfaLoginTempDataKey);
        }

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                return "your account";
            }

            var parts = email.Split('@', 2);
            var local = parts[0];
            if (local.Length <= 2)
            {
                return $"{local[0]}*@{parts[1]}";
            }

            return $"{local[0]}***{local[^1]}@{parts[1]}";
        }

        private async Task AddAuditLogAsync(string email, string name, string action, string description, string severity)
        {
            var ipAddress = ResolveClientIp(HttpContext);
            _context.AuditLogs.Add(new AuditLogEntry
            {
                timestamp = DateTime.UtcNow,
                user_email = email,
                user_name = string.IsNullOrWhiteSpace(name) ? email : name,
                action = action,
                entity = "Account",
                audit_type = "Security",
                severity = severity,
                ip_address = ipAddress,
                description = description
            });
            await _context.SaveChangesAsync();
        }

        private static string ResolveClientIp(HttpContext context)
        {
            return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        }

        private sealed class PendingMfaLoginState
        {
            public string UserId { get; set; } = string.Empty;
            public bool RememberMe { get; set; }
            public string? ReturnUrl { get; set; }
            public string LoginProvider { get; set; } = "password";
        }
    }
}
