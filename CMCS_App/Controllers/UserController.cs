using CMCS_App.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMCS_App.Controllers
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserController> _logger;

        public UserController(ApplicationDbContext context, ILogger<UserController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // If already logged in, redirect to appropriate dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToDashboard(User.FindFirst(ClaimTypes.Role)?.Value ?? "");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    TempData["ErrorMessage"] = "Email and password are required.";
                    return View();
                }

                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive);

                if (user == null || user.Password != password)
                {
                    TempData["ErrorMessage"] = "Invalid email or password.";
                    _logger.LogWarning($"Failed login attempt for email: {email}");
                    return View();
                }

                // Create claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                if (user.LecturerID.HasValue)
                    claims.Add(new Claim("LecturerID", user.LecturerID.Value.ToString()));
                if (user.CoordinatorID.HasValue)
                    claims.Add(new Claim("CoordinatorID", user.CoordinatorID.Value.ToString()));
                if (user.ManagerID.HasValue)
                    claims.Add(new Claim("ManagerID", user.ManagerID.Value.ToString()));

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation($"User logged in: {user.Email} ({user.Role})");

                TempData["SuccessMessage"] = $"Welcome back, {user.FullName}!";

                // Redirect based on role
                return RedirectToDashboard(user.Role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                TempData["ErrorMessage"] = "An error occurred during login. Please try again.";
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userName = User.Identity?.Name ?? "Unknown";
            
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            _logger.LogInformation($"User logged out: {userName}");
            
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            
            return RedirectToAction(nameof(Login));
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToDashboard(string role)
        {
            return role switch
            {
                "Lecturer" => RedirectToAction("Index", "Lecturer"),
                "Coordinator" => RedirectToAction("Index", "ProgrammeCoordinator"),
                "Manager" => RedirectToAction("Index", "AcademicManager"),
                "HR" => RedirectToAction("Index", "HR"),
                _ => RedirectToAction("Index", "Home")
            };
        }
    }
}