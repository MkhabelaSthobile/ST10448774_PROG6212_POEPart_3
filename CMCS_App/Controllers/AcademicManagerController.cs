using CMCS_App.Data;
using CMCS_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS_App.Controllers
{
    public class AcademicManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AcademicManagerController> _logger;

        public AcademicManagerController(ApplicationDbContext context, ILogger<AcademicManagerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            try
            {
                var claims = _context.Claims
                    .Include(c => c.Lecturer)
                    .Where(c => c.Status == "Approved by Coordinator" ||
                                c.Status == "Approved by Manager" ||
                                c.Status.Contains("Rejected by Manager"))
                    .OrderByDescending(c => c.SubmissionDate)
                    .ToList();

                _logger.LogInformation($"Loaded {claims.Count} claims for manager review");
                return View(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading manager dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard. Please check database connection.";
                return View(new List<Claim>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Verify(int id)
        {
            try
            {
                var claim = _context.Claims
                    .Include(c => c.Lecturer)
                    .FirstOrDefault(c => c.ClaimID == id);

                if (claim != null)
                {
                    claim.Status = "Approved by Manager";
                    claim.RejectionReason = null;
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = $"Claim #{claim.ClaimID} for {claim.Lecturer?.FullName} has been verified and approved for payment.";
                    _logger.LogInformation($"Claim {id} approved by manager");
                }
                else
                {
                    TempData["ErrorMessage"] = "Claim not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying claim: {ClaimId}", id);
                TempData["ErrorMessage"] = "Error verifying claim. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(int id, string rejectionReason)
        {
            try
            {
                var claim = _context.Claims
                    .Include(c => c.Lecturer)
                    .FirstOrDefault(c => c.ClaimID == id);

                if (claim != null)
                {
                    if (string.IsNullOrWhiteSpace(rejectionReason))
                    {
                        TempData["ErrorMessage"] = "Please provide a reason for rejection.";
                        return RedirectToAction(nameof(Index));
                    }

                    claim.Status = "Rejected by Manager";
                    claim.RejectionReason = rejectionReason.Trim();
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = $"Claim #{claim.ClaimID} for {claim.Lecturer?.FullName} has been rejected.";
                    _logger.LogInformation($"Claim {id} rejected by manager. Reason: {rejectionReason}");
                }
                else
                {
                    TempData["ErrorMessage"] = "Claim not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim: {ClaimId}", id);
                TempData["ErrorMessage"] = "Error rejecting claim. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateReport(string reportType)
        {
            try
            {
                var claims = _context.Claims
                    .Include(c => c.Lecturer)
                    .ToList();

                var reportData = new
                {
                    TotalClaims = claims.Count,
                    ApprovedClaims = claims.Count(c => c.Status == "Approved by Manager"),
                    PendingClaims = claims.Count(c => c.Status == "Approved by Coordinator"),
                    RejectedClaims = claims.Count(c => c.Status.Contains("Rejected")),
                    TotalAmount = claims.Where(c => c.Status == "Approved by Manager").Sum(c => c.TotalAmount),
                    GeneratedDate = DateTime.Now
                };

                TempData["SuccessMessage"] = $"Report Generated | Total: {reportData.TotalClaims} claims | " +
                    $"Approved: {reportData.ApprovedClaims} | Amount: R{reportData.TotalAmount:N2}";

                _logger.LogInformation($"Report generated: {reportData.TotalClaims} claims, R{reportData.TotalAmount:N2}");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                TempData["ErrorMessage"] = "Error generating report. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Details(int id)
        {
            try
            {
                var claim = _context.Claims
                    .Include(c => c.Lecturer)
                    .FirstOrDefault(c => c.ClaimID == id);

                if (claim == null)
                {
                    TempData["ErrorMessage"] = "Claim not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading claim details: {ClaimId}", id);
                TempData["ErrorMessage"] = "Error loading claim details.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClaim(int id)
        {
            try
            {
                var claim = await _context.Claims
                    .Include(c => c.Lecturer)
                    .FirstOrDefaultAsync(c => c.ClaimID == id);

                if (claim == null)
                {
                    TempData["ErrorMessage"] = "Claim not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Managers can delete claims approved by coordinator or those they've rejected
                if (claim.Status != "Approved by Coordinator" &&
                    !claim.Status.Contains("Rejected by Manager"))
                {
                    TempData["ErrorMessage"] = "Cannot delete this claim. Only claims pending manager approval or rejected by manager can be deleted.";
                    return RedirectToAction(nameof(Index));
                }

                var lecturerName = claim.Lecturer?.FullName ?? "Unknown";

                _context.Claims.Remove(claim);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Claim #{id} for {lecturerName} has been deleted successfully.";
                _logger.LogInformation($"Claim {id} deleted by manager");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting claim: {ClaimId}", id);
                TempData["ErrorMessage"] = "Error deleting claim. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}