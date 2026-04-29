using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DNASoftech.Web.Controllers
{
    /// <summary>
    /// MVC controller for user-facing account pages.
    /// Authentication (Login/Logout) is handled here as page actions;
    /// the actual credential check still calls the JSON API on the same app.
    /// Routes:
    ///   GET  /Account/Login    → login form
    ///   GET  /Account/Register → registration form
    ///   GET  /Account/Profile  → profile page (auth required)
    ///   GET  /Account/Orders   → order history (auth required)
    ///   POST /Account/Logout   → clears auth cookie, redirects to /
    /// </summary>
    public class AccountController : Controller
    {
        public IActionResult Login(string? returnUrl = null)
        {
            // If already logged in, redirect to home or return URL
            if (User?.Identity?.IsAuthenticated == true)
                return Redirect(returnUrl ?? "/");
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        public IActionResult Register()
        {
            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [Authorize]
        public IActionResult Profile()
        {
            return View();
        }

        [Authorize]
        public IActionResult EditProfile()
        {
            return View();
        }

        [Authorize]
        public IActionResult Orders()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CookieAuth");
            return RedirectToAction("Index", "Home");
        }
    }
}
