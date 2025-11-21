using CMCS_App.Data;
using CMCS_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS_App.Controllers
{
    public class ProgrammeCoordinatorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProgrammeCoordinatorController> _logger;

        public ProgrammeCoordinatorController(ApplicationDbContext context, ILogger<ProgrammeCoordinatorController> logger)
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
                    .OrderByDescending(c => c.SubmissionDate)
                    .ToList();

                _logger.LogInformation($"Loaded {claims.Count} claims for coordinator review");
                return View(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading coordinator dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard. Please check database connection.";
                return View(new List<Claim>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(int id)
        {
            try
            {
                var claim = _context.Claims
                    .Include(c => c.Lecturer)
                    .FirstOrDefault(c => c.ClaimID == id);

                if (claim != null)
                {
                    claim.Status = "Approved by Coordinator";
                    claim.RejectionReason = null;
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = $"Claim #{claim.ClaimID} for {claim.Lecturer?.FullName} has been approved.";
                    _logger.LogInformation($"Claim {id} approved by coordinator");
                }
                else
                {
                    TempData["ErrorMessage"] = "Claim not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim: {ClaimId}", id);
                TempData["ErrorMessage"] = "Error approving claim. Please try again.";
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

                    claim.Status = "Rejected by Coordinator";
                    claim.RejectionReason = rejectionReason.Trim();
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = $"Claim #{claim.ClaimID} for {claim.Lecturer?.FullName} has been rejected.";
                    _logger.LogInformation($"Claim {id} rejected by coordinator. Reason: {rejectionReason}");
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

                // Coordinators can only delete submitted/pending claims
                if (claim.Status != "Submitted" && claim.Status != "Pending")
                {
                    TempData["ErrorMessage"] = "Cannot delete claim that has already been approved or rejected.";
                    return RedirectToAction(nameof(Index));
                }

                var lecturerName = claim.Lecturer?.FullName ?? "Unknown";

                _context.Claims.Remove(claim);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Claim #{id} for {lecturerName} has been deleted successfully.";
                _logger.LogInformation($"Claim {id} deleted by coordinator");
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