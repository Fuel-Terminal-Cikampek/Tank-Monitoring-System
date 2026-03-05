using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TMS.Models;
using TMS.Web.Areas.Identity.Data;
using TMS.Web.Models;

namespace TMS.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;

        public AccountController(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        /// <summary>
        /// GET: /Account/Login
        /// Tampilkan halaman login
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        /// <summary>
        /// POST: /Account/Login
        /// Proses login dengan username dan password menggunakan ASP.NET Core Identity
        /// Supports legacy password formats dari aspnet_Membership:
        /// - Format 1: HMACSHA1 hashed with salt
        /// - Format 2: 3DES encrypted (CSF_CIKAMPEK key)
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            // Cari user berdasarkan username (case insensitive)
            var user = await _userManager.FindByNameAsync(model.Username);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Username atau password tidak valid.");
                return View(model);
            }

            // Attempt login menggunakan SignInManager
            // Custom LegacyPasswordHasher akan handle verifikasi password dari aspnet_Membership
            var result = await _signInManager.PasswordSignInAsync(
                model.Username,
                model.Password,
                isPersistent: model.RememberMe,
                lockoutOnFailure: false
            );

            if (result.Succeeded)
            {
                // Redirect ke return URL atau home page
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Akun Anda terkunci. Silakan hubungi administrator.");
                return View(model);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToAction("LoginWith2fa", new { returnUrl });
            }

            // Login gagal
            ModelState.AddModelError(string.Empty, "Username atau password tidak valid.");
            return View(model);
        }

        /// <summary>
        /// GET: /Account/Logout
        /// Logout user
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// GET: /Account/AccessDenied
        /// Halaman akses ditolak
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
