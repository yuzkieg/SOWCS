using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace IT15_SOWCS.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> signInManager;
        private readonly UserManager<Users> userManager;

        public AccountController(SignInManager<Users> signInManager, UserManager<Users> userManager)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
        }

        public IActionResult Login()
        {
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
                else
                {
                    ModelState.AddModelError("", "Wrong email or password.");
                    return View(model);
                }
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
                Users user = new Users
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
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }
            }
            return View(model);

        }
        public IActionResult VerifyEmail()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> VerifyEmail(VerifyViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    ModelState.AddModelError("", "Something went wrong"); 
                    return View(model);
                }
                else
                {
                    return RedirectToAction("ChangePassword", "Account", new {username = user.UserName});
                }
            }
            return View(model);
        }
        public IActionResult ChangePassword(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("VerifyEmail", "Account");
            }
            return View(new ChangePasswordViewModel { Email = username }); 
        }
        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var result = await userManager.RemovePasswordAsync(user);
                    if (result.Succeeded)
                    {
                        result = await userManager.AddPasswordAsync(user, model.NewPassword);
                        return RedirectToAction("Login", "Account");
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        return View(model);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "User not found");
                    return View(model);
                }
            }
            else
            {
                ModelState.AddModelError("", "Something went wrong. Try again.");
                return View(model);
            }
        }
        [HttpPost]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            // Optional: validate returnUrl to prevent open-redirect attacks
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = Url.Action("Index", "Dashboard");  // fallback

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                // Better error handling
                TempData["Error"] = $"Error from external provider: {remoteError}";
                return RedirectToAction(nameof(Login));
            }

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["Error"] = "Error loading external login information.";
                return RedirectToAction(nameof(Login));
            }

            // Sign in with external login if user already linked
            var result = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false);

            if (result.Succeeded)
            {
                await signInManager.UpdateExternalAuthenticationTokensAsync(info);

                // Redirect to original page or fallback to dashboard
                returnUrl = returnUrl ?? Url.Action("Index", "Dashboard");
                return LocalRedirect(returnUrl);   // ← use LocalRedirect for safety
            }

            // New user flow
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
                var fullName = info.Principal.FindFirstValue(ClaimTypes.Name); // fallback - usually "First Last"

                // Use whatever combination you prefer
                var displayName = !string.IsNullOrEmpty(fullName)
                    ? fullName
                    : $"{givenName ?? ""} {familyName ?? ""}".Trim()
                      ?? email.Split('@')[0];  // worst-case fallback to part before @

                user = new Users
                {
                    UserName = email,
                    Email = email,
                    FullName = displayName,          // ← now set!
                    EmailConfirmed = true                  // Google already verified email
                };

                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    foreach (var err in createResult.Errors)
                        ModelState.AddModelError("", err.Description);
                    // Optionally log or TempData["Error"] = ...
                    return View("Login");
                }

                await userManager.AddLoginAsync(user, info);
            }

            // Sign the new/existing user in
            await signInManager.SignInAsync(user, isPersistent: false);
            await signInManager.UpdateExternalAuthenticationTokensAsync(info);

            // Redirect with returnUrl support
            returnUrl = returnUrl ?? Url.Action("Index", "Dashboard");
            return LocalRedirect(returnUrl);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            // Redirecting to Login ensures a clean session end
            return RedirectToAction("Login", "Account");
        }
    }
}
