using CMCS_App.Data;
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

        // HR Dashboard with automated statistics
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

        // Generate invoice report
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

        // Generate payment summary
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

        // Export data to CSV
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

        // View lecturer performance
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

        // View monthly financial report
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

        // Get claims requiring attention
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

        // Manage lecturer data
        [HttpGet]
        public async Task<IActionResult> ManageLecturers()
        {
            try
            {
                var lecturers = await _context.Lecturers.ToListAsync();
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

        // Update lecturer information
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLecturer(int lecturerId, string fullName, string email, decimal hourlyRate)
        {
            try
            {
                var lecturer = await _context.Lecturers.FindAsync(lecturerId);
                if (lecturer == null)
                {
                    TempData["ErrorMessage"] = "Lecturer not found.";
                    return RedirectToAction(nameof(ManageLecturers));
                }

                lecturer.FullName = fullName.Trim();
                lecturer.Email = email.Trim();
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

        // Process batch payments
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessBatchPayment(string month)
        {
            try
            {
                var claims = await _context.Claims
                    .Where(c => c.Month == month && c.Status == "Approved by Manager")
                    .ToListAsync();

                if (!claims.Any())
                {
                    TempData["ErrorMessage"] = "No approved claims found for the selected month.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var claim in claims)
                {
                    claim.Status = "Payment Processed";
                }

                await _context.SaveChangesAsync();

                var totalAmount = claims.Sum(c => c.TotalAmount);
                TempData["SuccessMessage"] = $"Batch payment processed for {claims.Count} claims. Total: R{totalAmount:N2}";
                _logger.LogInformation("Batch payment processed for {Month}: {ClaimCount} claims, R{TotalAmount}",
                    month, claims.Count, totalAmount);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch payment for {Month}", month);
                TempData["ErrorMessage"] = "Error processing batch payment.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Generate comprehensive annual report
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
    }
}