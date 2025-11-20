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

                // Normalize email
                email = email.ToLower().Trim();

                string userName = "";
                string role = "";
                int? lecturerId = null;
                int? coordinatorId = null;
                int? managerId = null;

                // 1. Check HR Users table first
                var hrUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.IsActive && u.Role == "HR");

                if (hrUser != null)
                {
                    if (hrUser.Password == password)
                    {
                        userName = hrUser.FullName;
                        role = "HR";
                        _logger.LogInformation($"HR user logged in: {email}");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Invalid email or password.";
                        _logger.LogWarning($"Failed login attempt for HR user: {email}");
                        return View();
                    }
                }
                // 2. Check Lecturers table
                else
                {
                    var lecturer = await _context.Lecturers
                        .FirstOrDefaultAsync(l => l.Email.ToLower() == email);

                    if (lecturer != null)
                    {
                        if (lecturer.Password == password)
                        {
                            userName = lecturer.FullName;
                            role = "Lecturer";
                            lecturerId = lecturer.LecturerID;
                            _logger.LogInformation($"Lecturer logged in: {email}");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Invalid email or password.";
                            _logger.LogWarning($"Failed login attempt for lecturer: {email}");
                            return View();
                        }
                    }
                    // 3. Check ProgrammeCoordinators table
                    else
                    {
                        var coordinator = await _context.ProgrammeCoordinators
                            .FirstOrDefaultAsync(c => c.Email.ToLower() == email);

                        if (coordinator != null)
                        {
                            if (coordinator.Password == password)
                            {
                                userName = coordinator.Name;
                                role = "Coordinator";
                                coordinatorId = coordinator.CoordinatorID;
                                _logger.LogInformation($"Coordinator logged in: {email}");
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Invalid email or password.";
                                _logger.LogWarning($"Failed login attempt for coordinator: {email}");
                                return View();
                            }
                        }
                        // 4. Check AcademicManagers table
                        else
                        {
                            var manager = await _context.AcademicManagers
                                .FirstOrDefaultAsync(m => m.Email.ToLower() == email);

                            if (manager != null)
                            {
                                if (manager.Password == password)
                                {
                                    userName = manager.FullName;
                                    role = "Manager";
                                    managerId = manager.ManagerID;
                                    _logger.LogInformation($"Manager logged in: {email}");
                                }
                                else
                                {
                                    TempData["ErrorMessage"] = "Invalid email or password.";
                                    _logger.LogWarning($"Failed login attempt for manager: {email}");
                                    return View();
                                }
                            }
                            else
                            {
                                // User not found in any table
                                TempData["ErrorMessage"] = "Invalid email or password.";
                                _logger.LogWarning($"Login attempt with unknown email: {email}");
                                return View();
                            }
                        }
                    }
                }

                // Create claims for authenticated user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, userName),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Role, role)
                };

                if (lecturerId.HasValue)
                    claims.Add(new Claim("LecturerID", lecturerId.Value.ToString()));
                if (coordinatorId.HasValue)
                    claims.Add(new Claim("CoordinatorID", coordinatorId.Value.ToString()));
                if (managerId.HasValue)
                    claims.Add(new Claim("ManagerID", managerId.Value.ToString()));

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

                TempData["SuccessMessage"] = $"Welcome back, {userName}!";

                // Redirect based on role
                return RedirectToDashboard(role);
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
                _ => RedirectToAction(nameof(Login))
            };
        }

        /// <summary>
        /// Helper action to get all login credentials for display (for testing only)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllCredentials()
        {
            var credentials = new
            {
                HRAdmins = await _context.Users
                    .Where(u => u.Role == "HR")
                    .Select(u => new { u.Email, u.Password, Role = "HR" })
                    .ToListAsync(),

                Lecturers = await _context.Lecturers
                    .Select(l => new { l.Email, l.Password, Role = "Lecturer" })
                    .ToListAsync(),

                Coordinators = await _context.ProgrammeCoordinators
                    .Select(c => new { c.Email, c.Password, Role = "Coordinator" })
                    .ToListAsync(),

                Managers = await _context.AcademicManagers
                    .Select(m => new { m.Email, m.Password, Role = "Manager" })
                    .ToListAsync()
            };

            return Json(credentials);
        }
    }
}