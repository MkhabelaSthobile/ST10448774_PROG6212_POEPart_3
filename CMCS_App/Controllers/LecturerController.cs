using Microsoft.AspNetCore.Mvc;
using CMCS_App.Models;
using CMCS_App.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMCS_App.Controllers
{
    public class LecturerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LecturerController> _logger;
        private readonly IWebHostEnvironment _environment;

        public LecturerController(ApplicationDbContext context, ILogger<LecturerController> logger, IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        public IActionResult Index()
        {
            try
            {
                // Get current lecturer ID from claims
                var lecturerIdClaim = User.FindFirst("LecturerID")?.Value;

                if (string.IsNullOrEmpty(lecturerIdClaim) || !int.TryParse(lecturerIdClaim, out int currentLecturerId))
                {
                    TempData["ErrorMessage"] = "Unable to identify lecturer. Please log in again.";
                    return RedirectToAction("Login", "User");
                }

                // Get lecturer info for hourly rate
                var lecturer = _context.Lecturers.FirstOrDefault(l => l.LecturerID == currentLecturerId);
                if (lecturer != null)
                {
                    ViewBag.LecturerName = lecturer.FullName;
                    ViewBag.HourlyRate = lecturer.HourlyRate;
                    ViewBag.ModuleName = lecturer.ModuleName;
                }

                var claims = _context.Claims
                    .Include(c => c.Lecturer)
                    .Where(c => c.LecturerID == currentLecturerId)
                    .OrderByDescending(c => c.SubmissionDate)
                    .ToList();

                _logger.LogInformation($"Loaded {claims.Count} claims for lecturer {currentLecturerId}");
                return View(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lecturer dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard. Please check database connection.";
                return View(new List<Models.Claim>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClaim(string moduleName, string month, int hoursWorked, IFormFile supportingDocument)
        {
            try
            {
                // Get current lecturer ID from claims
                var lecturerIdClaim = User.FindFirst("LecturerID")?.Value;

                if (string.IsNullOrEmpty(lecturerIdClaim) || !int.TryParse(lecturerIdClaim, out int currentLecturerId))
                {
                    TempData["ErrorMessage"] = "Unable to identify lecturer. Please log in again.";
                    return RedirectToAction("Login", "User");
                }

                // Get lecturer's hourly rate from database
                var lecturer = await _context.Lecturers.FindAsync(currentLecturerId);

                if (lecturer == null)
                {
                    TempData["ErrorMessage"] = "Lecturer not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(moduleName) || string.IsNullOrWhiteSpace(month))
                {
                    TempData["ErrorMessage"] = "Module name and month are required.";
                    return RedirectToAction(nameof(Index));
                }

                if (hoursWorked < 1 || hoursWorked > 200)
                {
                    TempData["ErrorMessage"] = "Hours worked must be between 1 and 200.";
                    return RedirectToAction(nameof(Index));
                }

                var claim = new Models.Claim
                {
                    LecturerID = currentLecturerId,
                    ModuleName = moduleName.Trim(),
                    Month = month,
                    HoursWorked = hoursWorked,
                    HourlyRate = lecturer.HourlyRate, // Use lecturer's registered hourly rate
                    SubmissionDate = DateTime.Now,
                    Status = "Submitted"
                };

                claim.TotalAmount = claim.CalculateTotal();

                if (supportingDocument != null && supportingDocument.Length > 0)
                {
                    var uploadedFileName = await HandleFileUpload(supportingDocument);
                    if (!string.IsNullOrEmpty(uploadedFileName))
                    {
                        claim.SupportingDocument = uploadedFileName;
                    }
                    else if (ModelState.ContainsKey("SupportingDocument"))
                    {
                        TempData["ErrorMessage"] = ModelState["SupportingDocument"].Errors.FirstOrDefault()?.ErrorMessage;
                        return RedirectToAction(nameof(Index));
                    }
                }

                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Claim submitted successfully! Total Amount: R{claim.TotalAmount:N2}" +
                    (claim.SupportingDocument != null ? " | Document uploaded." : "");

                _logger.LogInformation($"Claim {claim.ClaimID} submitted successfully by lecturer {currentLecturerId}");
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error submitting claim");
                TempData["ErrorMessage"] = "Database error. Please check your input and try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim");
                TempData["ErrorMessage"] = "An error occurred while submitting your claim. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<string> HandleFileUpload(IFormFile supportingDocument)
        {
            if (supportingDocument == null || supportingDocument.Length == 0)
                return null;

            try
            {
                if (supportingDocument.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("SupportingDocument", "File size cannot exceed 5MB.");
                    return null;
                }

                var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".jpg", ".png", ".jpeg" };
                var fileExtension = Path.GetExtension(supportingDocument.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("SupportingDocument",
                        "Invalid file type. Allowed: PDF, DOCX, XLSX, JPG, PNG.");
                    return null;
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var originalFileName = Path.GetFileNameWithoutExtension(supportingDocument.FileName);
                var uniqueFileName = $"{originalFileName}_{Guid.NewGuid():N}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await supportingDocument.CopyToAsync(fileStream);
                }

                _logger.LogInformation($"File uploaded: {uniqueFileName}, Size: {supportingDocument.Length} bytes");
                return uniqueFileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file: {FileName}", supportingDocument.FileName);
                ModelState.AddModelError("SupportingDocument", "Error uploading file. Please try again.");
                return null;
            }
        }

        public async Task<IActionResult> DownloadDocument(int claimId, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return NotFound();
            }

            try
            {
                var lecturerIdClaim = User.FindFirst("LecturerID")?.Value;

                if (string.IsNullOrEmpty(lecturerIdClaim) || !int.TryParse(lecturerIdClaim, out int currentLecturerId))
                {
                    return Unauthorized();
                }

                var claim = await _context.Claims
                    .FirstOrDefaultAsync(c => c.ClaimID == claimId && c.LecturerID == currentLecturerId);

                if (claim == null || claim.SupportingDocument != fileName)
                {
                    return NotFound("File not found or access denied.");
                }

                var filePath = Path.Combine(_environment.WebRootPath, "uploads", fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("File not found on server.");
                }

                var memory = new MemoryStream();
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    await stream.CopyToAsync(memory);
                }
                memory.Position = 0;

                var contentType = GetContentType(fileName);
                var originalFileName = fileName.Contains('_') ?
                    fileName.Substring(0, fileName.LastIndexOf('_')) + Path.GetExtension(fileName) :
                    fileName;

                return File(memory, contentType, originalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file: {FileName}", fileName);
                TempData["ErrorMessage"] = "Error downloading file. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClaim(int id)
        {
            try
            {
                var lecturerIdClaim = User.FindFirst("LecturerID")?.Value;

                if (string.IsNullOrEmpty(lecturerIdClaim) || !int.TryParse(lecturerIdClaim, out int currentLecturerId))
                {
                    return Unauthorized();
                }

                var claim = await _context.Claims
                    .FirstOrDefaultAsync(c => c.ClaimID == id && c.LecturerID == currentLecturerId);

                if (claim == null)
                {
                    TempData["ErrorMessage"] = "Claim not found or you don't have permission to delete it.";
                    return RedirectToAction(nameof(Index));
                }

                if (claim.Status != "Submitted")
                {
                    TempData["ErrorMessage"] = "Cannot delete claim that has already been processed.";
                    return RedirectToAction(nameof(Index));
                }

                if (!string.IsNullOrEmpty(claim.SupportingDocument))
                {
                    var filePath = Path.Combine(_environment.WebRootPath, "uploads", claim.SupportingDocument);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                _context.Claims.Remove(claim);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Claim deleted successfully.";
                _logger.LogInformation($"Claim {id} deleted by lecturer {currentLecturerId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting claim: {ClaimId}", id);
                TempData["ErrorMessage"] = "Error deleting claim. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var lecturerIdClaim = User.FindFirst("LecturerID")?.Value;

                if (string.IsNullOrEmpty(lecturerIdClaim) || !int.TryParse(lecturerIdClaim, out int currentLecturerId))
                {
                    return Unauthorized();
                }

                var claim = await _context.Claims
                    .Include(c => c.Lecturer)
                    .FirstOrDefaultAsync(c => c.ClaimID == id && c.LecturerID == currentLecturerId);

                if (claim == null)
                {
                    TempData["ErrorMessage"] = "Claim not found or you don't have permission to view it.";
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
    }
}