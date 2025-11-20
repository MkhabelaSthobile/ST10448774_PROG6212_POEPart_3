using CMCS_App.Data;
using CMCS_App.Models;
using CMCS_App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS_App.Controllers
{
    public class HRController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHRReportService _reportService;
        private readonly IClaimAutomationService _automationService;
        private readonly ILogger<HRController> _logger;

        public HRController(
            ApplicationDbContext context,
            IHRReportService reportService,
            IClaimAutomationService automationService,
            ILogger<HRController> logger)
        {
            _context = context;
            _reportService = reportService;
            _automationService = automationService;
            _logger = logger;
        }

        /// <summary>
        /// HR Dashboard with automated statistics
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var statistics = await _automationService.GenerateStatisticsAsync();
                _logger.LogInformation("HR Dashboard loaded with {ClaimCount} total claims", statistics.TotalClaims);
                return View(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HR dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard. Please try again.";
                return View();
            }
        }

        /// <summary>
        /// Generate invoice report
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GenerateInvoiceReport(int? lecturerId, string? month)
        {
            try
            {
                var report = await _reportService.GenerateInvoiceReportAsync(lecturerId, month);
                _logger.LogInformation("Invoice report generated for Lecturer: {LecturerId}, Month: {Month}",
                    lecturerId ?? 0, month ?? "All");

                return Content(report, "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice report");
                TempData["ErrorMessage"] = "Error generating invoice report.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Generate payment summary
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GeneratePaymentSummary(string month)
        {
            try
            {
                if (string.IsNullOrEmpty(month))
                {
                    TempData["ErrorMessage"] = "Please select a month.";
                    return RedirectToAction(nameof(Index));
                }

                var report = await _reportService.GeneratePaymentSummaryAsync(month);
                _logger.LogInformation("Payment summary generated for {Month}", month);

                return Content(report, "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating payment summary");
                TempData["ErrorMessage"] = "Error generating payment summary.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Export data to CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportToCSV(string reportType)
        {
            try
            {
                var filters = new Dictionary<string, string>();
                var csvData = await _reportService.ExportToCSVAsync(reportType, filters);

                var fileName = $"{reportType}_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                _logger.LogInformation("CSV export generated: {FileName}", fileName);

                return File(csvData, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to CSV");
                TempData["ErrorMessage"] = "Error exporting data. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// View lecturer performance
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> LecturerPerformance(int lecturerId)
        {
            try
            {
                var report = await _reportService.GenerateLecturerPerformanceReportAsync(lecturerId);
                _logger.LogInformation("Performance report generated for Lecturer {LecturerId}", lecturerId);
                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating lecturer performance report");
                TempData["ErrorMessage"] = "Error generating performance report.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// View monthly financial report
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> MonthlyFinancialReport(string month)
        {
            try
            {
                if (string.IsNullOrEmpty(month))
                {
                    TempData["ErrorMessage"] = "Please select a month.";
                    return RedirectToAction(nameof(Index));
                }

                var report = await _reportService.GenerateMonthlyFinancialReportAsync(month);
                _logger.LogInformation("Monthly financial report generated for {Month}", month);
                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly financial report");
                TempData["ErrorMessage"] = "Error generating financial report.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Get claims requiring attention
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ClaimsRequiringAttention()
        {
            try
            {
                var claims = await _automationService.GetClaimsRequiringAttentionAsync("hr");
                _logger.LogInformation("{ClaimCount} claims requiring HR attention", claims.Count);
                return View(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading claims requiring attention");
                TempData["ErrorMessage"] = "Error loading claims.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Manage lecturer data
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ManageLecturers()
        {
            try
            {
                var lecturers = await _context.Lecturers.ToListAsync();

                // Get claim counts and earnings for each lecturer
                var lecturerClaims = new Dictionary<int, int>();
                var lecturerEarnings = new Dictionary<int, decimal>();

                foreach (var lecturer in lecturers)
                {
                    var claims = await _context.Claims
                        .Where(c => c.LecturerID == lecturer.LecturerID)
                        .ToListAsync();

                    lecturerClaims[lecturer.LecturerID] = claims.Count;
                    lecturerEarnings[lecturer.LecturerID] = claims
                        .Where(c => c.Status == "Approved by Manager")
                        .Sum(c => c.TotalAmount);
                }

                ViewBag.LecturerClaims = lecturerClaims;
                ViewBag.LecturerEarnings = lecturerEarnings;

                _logger.LogInformation("Loaded {LecturerCount} lecturers for management", lecturers.Count);
                return View(lecturers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lecturers");
                TempData["ErrorMessage"] = "Error loading lecturer data.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Add new lecturer
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLecturer(string fullName, string email, string moduleName, decimal hourlyRate)
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(moduleName))
                {
                    TempData["ErrorMessage"] = "All fields are required.";
                    return RedirectToAction(nameof(ManageLecturers));
                }

                if (hourlyRate < 0.01m || hourlyRate > 1000m)
                {
                    TempData["ErrorMessage"] = "Hourly rate must be between R0.01 and R1000.";
                    return RedirectToAction(nameof(ManageLecturers));
                }

                // Check for duplicate email
                var existingLecturer = await _context.Lecturers
                    .FirstOrDefaultAsync(l => l.Email.ToLower() == email.ToLower());

                if (existingLecturer != null)
                {
                    TempData["ErrorMessage"] = "A lecturer with this email already exists.";
                    return RedirectToAction(nameof(ManageLecturers));
                }

                // Generate default password
                var defaultPassword = GeneratePassword(fullName);

                // Create new lecturer
                var lecturer = new Lecturer
                {
                    FullName = fullName.Trim(),
                    Email = email.Trim(),
                    ModuleName = moduleName.Trim(),
                    HourlyRate = hourlyRate,
                    Password = defaultPassword // In production, hash this password!
                };

                _context.Lecturers.Add(lecturer);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Lecturer {fullName} added successfully! Default password: {defaultPassword}";
                _logger.LogInformation("New lecturer added: {LecturerName} (ID: {LecturerId})", fullName, lecturer.LecturerID);

                return RedirectToAction(nameof(ManageLecturers));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new lecturer");
                TempData["ErrorMessage"] = "Error adding lecturer. Please try again.";
                return RedirectToAction(nameof(ManageLecturers));
            }
        }

        /// <summary>
        /// Update lecturer information
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLecturer(int lecturerId, string fullName, string email, string moduleName, decimal hourlyRate)
        {
            try
            {
                var lecturer = await _context.Lecturers.FindAsync(lecturerId);
                if (lecturer == null)
                {
                    TempData["ErrorMessage"] = "Lecturer not found.";
                    return RedirectToAction(nameof(ManageLecturers));
                }

                // Validation
                if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(moduleName))
                {
                    TempData["ErrorMessage"] = "All fields are required.";
                    return RedirectToAction(nameof(ManageLecturers));
                }

                if (hourlyRate < 0.01m || hourlyRate > 1000m)
                {
                    TempData["ErrorMessage"] = "Hourly rate must be between R0.01 and R1000.";
                    return RedirectToAction(nameof(ManageLecturers));
                }

                // Check for duplicate email (excluding current lecturer)
                var existingLecturer = await _context.Lecturers
                    .FirstOrDefaultAsync(l => l.Email.ToLower() == email.ToLower() && l.LecturerID != lecturerId);

                if (existingLecturer != null)
                {
                    TempData["ErrorMessage"] = "Another lecturer with this email already exists.";
                    return RedirectToAction(nameof(ManageLecturers));
                }

                lecturer.FullName = fullName.Trim();
                lecturer.Email = email.Trim();
                lecturer.ModuleName = moduleName.Trim();
                lecturer.HourlyRate = hourlyRate;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Lecturer {fullName} updated successfully.";
                _logger.LogInformation("Lecturer {LecturerId} updated by HR", lecturerId);

                return RedirectToAction(nameof(ManageLecturers));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating lecturer {LecturerId}", lecturerId);
                TempData["ErrorMessage"] = "Error updating lecturer information.";
                return RedirectToAction(nameof(ManageLecturers));
            }
        }

        /// <summary>
        /// Process batch payments
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessBatchPayment(string month)
        {
            try
            {
                if (string.IsNullOrEmpty(month))
                {
                    TempData["ErrorMessage"] = "Please select a month.";
                    return RedirectToAction(nameof(Index));
                }

                var claims = await _context.Claims
                    .Include(c => c.Lecturer)
                    .Where(c => c.Month == month && c.Status == "Approved by Manager")
                    .ToListAsync();

                if (!claims.Any())
                {
                    TempData["ErrorMessage"] = "No approved claims found for the selected month.";
                    return RedirectToAction(nameof(Index));
                }

                // In production, this would integrate with payment gateway
                // For now, we'll log the payment processing
                var totalAmount = claims.Sum(c => c.TotalAmount);

                TempData["SuccessMessage"] = $"Batch payment processed for {claims.Count} claims totaling R{totalAmount:N2}. " +
                    $"Payment details have been generated for {month}.";

                _logger.LogInformation("Batch payment processed for {Month}: {ClaimCount} claims, R{TotalAmount}",
                    month, claims.Count, totalAmount);

                // Log each payment
                foreach (var claim in claims)
                {
                    _logger.LogInformation("Payment: Lecturer {LecturerName} - Claim #{ClaimId} - R{Amount}",
                        claim.Lecturer?.FullName, claim.ClaimID, claim.TotalAmount);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch payment for {Month}", month);
                TempData["ErrorMessage"] = "Error processing batch payment.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Generate comprehensive annual report
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GenerateAnnualReport(int year)
        {
            try
            {
                var claims = await _context.Claims
                    .Include(c => c.Lecturer)
                    .Where(c => c.SubmissionDate.Year == year)
                    .ToListAsync();

                var report = new
                {
                    Year = year,
                    TotalClaims = claims.Count,
                    TotalAmountPaid = claims.Where(c => c.Status == "Approved by Manager").Sum(c => c.TotalAmount),
                    UniqueLecturers = claims.Select(c => c.LecturerID).Distinct().Count(),
                    TotalHours = claims.Where(c => c.Status == "Approved by Manager").Sum(c => c.HoursWorked),
                    MonthlyBreakdown = claims.GroupBy(c => c.Month)
                        .Select(g => new
                        {
                            Month = g.Key,
                            Claims = g.Count(),
                            Amount = g.Sum(c => c.TotalAmount)
                        })
                        .OrderBy(m => m.Month)
                        .ToList()
                };

                _logger.LogInformation("Annual report generated for year {Year}", year);
                return Json(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating annual report for year {Year}", year);
                return Json(new { error = "Error generating annual report" });
            }
        }

        /// <summary>
        /// Generate default password for new lecturer
        /// </summary>
        private string GeneratePassword(string fullName)
        {
            // Simple password generation - in production, use more secure method
            var nameParts = fullName.Split(' ');
            var firstInitial = nameParts[0].Substring(0, 1).ToUpper();
            var lastName = nameParts.Length > 1 ? nameParts[nameParts.Length - 1] : nameParts[0];
            var random = new Random().Next(100, 999);

            return $"{firstInitial}{lastName}{random}";
        }
    }
}