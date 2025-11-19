using CMCS_App.Data;
using CMCS_App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMCS_App.Services
{
    /// <summary>
    /// Service for automating claim verification and approval workflows
    /// </summary>
    public interface IClaimAutomationService
    {
        Task<ClaimValidationResult> ValidateClaimAsync(Claim claim);
        Task<ClaimValidationResult> AutoVerifyClaimAsync(int claimId);
        Task<List<Claim>> GetClaimsRequiringAttentionAsync(string role);
        Task<ClaimStatistics> GenerateStatisticsAsync();
        Task NotifyStakeholdersAsync(Claim claim, string action);
    }

    public class ClaimAutomationService : IClaimAutomationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClaimAutomationService> _logger;

        // Automation rules
        private const decimal MAX_HOURLY_RATE = 1000m;
        private const int MAX_HOURS_PER_MONTH = 200;
        private const int STANDARD_WORKING_HOURS = 160; // 40 hours/week * 4 weeks
        private const decimal AUTO_APPROVE_THRESHOLD = 10000m; // Claims under this can be auto-approved

        public ClaimAutomationService(ApplicationDbContext context, ILogger<ClaimAutomationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Validates a claim against predefined business rules
        /// </summary>
        public async Task<ClaimValidationResult> ValidateClaimAsync(Claim claim)
        {
            var result = new ClaimValidationResult
            {
                IsValid = true,
                ClaimId = claim.ClaimID,
                Warnings = new List<string>(),
                Errors = new List<string>(),
                Recommendations = new List<string>()
            };

            try
            {
                // Rule 1: Check hours worked
                if (claim.HoursWorked < 1 || claim.HoursWorked > MAX_HOURS_PER_MONTH)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Hours worked ({claim.HoursWorked}) must be between 1 and {MAX_HOURS_PER_MONTH}");
                }
                else if (claim.HoursWorked > STANDARD_WORKING_HOURS)
                {
                    result.Warnings.Add($"Hours worked ({claim.HoursWorked}) exceeds standard monthly hours ({STANDARD_WORKING_HOURS}). Requires justification.");
                }

                // Rule 2: Check hourly rate
                if (claim.HourlyRate < 0.01m || claim.HourlyRate > MAX_HOURLY_RATE)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Hourly rate (R{claim.HourlyRate}) must be between R0.01 and R{MAX_HOURLY_RATE}");
                }

                // Rule 3: Verify lecturer exists and rate matches
                var lecturer = await _context.Lecturers.FindAsync(claim.LecturerID);
                if (lecturer == null)
                {
                    result.IsValid = false;
                    result.Errors.Add("Lecturer not found in system");
                }
                else if (Math.Abs(lecturer.HourlyRate - claim.HourlyRate) > 0.01m)
                {
                    result.Warnings.Add($"Claim hourly rate (R{claim.HourlyRate}) differs from lecturer's registered rate (R{lecturer.HourlyRate})");
                }

                // Rule 4: Check for duplicate claims
                var duplicateClaim = await _context.Claims
                    .FirstOrDefaultAsync(c =>
                        c.LecturerID == claim.LecturerID &&
                        c.Month == claim.Month &&
                        c.ClaimID != claim.ClaimID &&
                        !c.Status.Contains("Rejected"));

                if (duplicateClaim != null)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Duplicate claim found for {claim.Month}. Claim #{duplicateClaim.ClaimID} already exists.");
                }

                // Rule 5: Check total amount calculation
                var calculatedTotal = claim.HoursWorked * claim.HourlyRate;
                if (Math.Abs(claim.TotalAmount - calculatedTotal) > 0.01m)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Total amount mismatch. Expected: R{calculatedTotal:N2}, Got: R{claim.TotalAmount:N2}");
                }

                // Rule 6: Supporting document recommendation
                if (string.IsNullOrEmpty(claim.SupportingDocument) && claim.TotalAmount > 5000m)
                {
                    result.Recommendations.Add("Supporting document recommended for claims over R5,000");
                }

                // Rule 7: Auto-approval eligibility
                if (claim.TotalAmount <= AUTO_APPROVE_THRESHOLD &&
                    claim.HoursWorked <= STANDARD_WORKING_HOURS &&
                    result.Errors.Count == 0 &&
                    result.Warnings.Count == 0)
                {
                    result.CanAutoApprove = true;
                    result.Recommendations.Add($"Claim eligible for automatic approval (under R{AUTO_APPROVE_THRESHOLD:N2} threshold)");
                }

                _logger.LogInformation($"Claim {claim.ClaimID} validation completed. Valid: {result.IsValid}, Warnings: {result.Warnings.Count}, Errors: {result.Errors.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating claim {claim.ClaimID}");
                result.IsValid = false;
                result.Errors.Add("System error during validation. Please contact support.");
            }

            return result;
        }

        /// <summary>
        /// Automatically verifies and approves/rejects claims based on business rules
        /// </summary>
        public async Task<ClaimValidationResult> AutoVerifyClaimAsync(int claimId)
        {
            var claim = await _context.Claims
                .Include(c => c.Lecturer)
                .FirstOrDefaultAsync(c => c.ClaimID == claimId);

            if (claim == null)
            {
                return new ClaimValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Claim not found" }
                };
            }

            var validationResult = await ValidateClaimAsync(claim);

            // Auto-approve if eligible
            if (validationResult.CanAutoApprove && claim.Status == "Submitted")
            {
                claim.Status = "Approved by Coordinator";
                claim.RejectionReason = null;
                await _context.SaveChangesAsync();

                validationResult.ActionTaken = "Auto-approved by system";
                _logger.LogInformation($"Claim {claimId} auto-approved");

                await NotifyStakeholdersAsync(claim, "auto-approved");
            }
            // Auto-reject if critical errors
            else if (validationResult.Errors.Count > 0)
            {
                claim.Status = "Rejected by Coordinator";
                claim.RejectionReason = "Automatic rejection: " + string.Join("; ", validationResult.Errors);
                await _context.SaveChangesAsync();

                validationResult.ActionTaken = "Auto-rejected due to validation errors";
                _logger.LogWarning($"Claim {claimId} auto-rejected: {claim.RejectionReason}");

                await NotifyStakeholdersAsync(claim, "auto-rejected");
            }
            // Flag for manual review if warnings exist
            else if (validationResult.Warnings.Count > 0)
            {
                validationResult.ActionTaken = "Flagged for manual review due to warnings";
                _logger.LogInformation($"Claim {claimId} flagged for manual review");
            }

            return validationResult;
        }

        /// <summary>
        /// Gets claims that require attention based on user role
        /// </summary>
        public async Task<List<Claim>> GetClaimsRequiringAttentionAsync(string role)
        {
            var query = _context.Claims.Include(c => c.Lecturer).AsQueryable();

            switch (role.ToLower())
            {
                case "coordinator":
                    // Get submitted claims and those with warnings
                    query = query.Where(c => c.Status == "Submitted" || c.Status == "Pending");
                    break;

                case "manager":
                    // Get coordinator-approved claims
                    query = query.Where(c => c.Status == "Approved by Coordinator");
                    break;

                case "hr":
                    // Get manager-approved claims ready for payment
                    query = query.Where(c => c.Status == "Approved by Manager");
                    break;

                default:
                    return new List<Claim>();
            }

            return await query
                .OrderBy(c => c.SubmissionDate)
                .ToListAsync();
        }

        /// <summary>
        /// Generates comprehensive statistics for reporting
        /// </summary>
        public async Task<ClaimStatistics> GenerateStatisticsAsync()
        {
            var allClaims = await _context.Claims.Include(c => c.Lecturer).ToListAsync();

            var statistics = new ClaimStatistics
            {
                TotalClaims = allClaims.Count,
                SubmittedClaims = allClaims.Count(c => c.Status == "Submitted" || c.Status == "Pending"),
                ApprovedByCoordinator = allClaims.Count(c => c.Status == "Approved by Coordinator"),
                ApprovedByManager = allClaims.Count(c => c.Status == "Approved by Manager"),
                RejectedClaims = allClaims.Count(c => c.Status.Contains("Rejected")),

                TotalAmountClaimed = allClaims.Sum(c => c.TotalAmount),
                TotalAmountApproved = allClaims.Where(c => c.Status == "Approved by Manager").Sum(c => c.TotalAmount),
                TotalAmountPending = allClaims.Where(c => !c.Status.Contains("Approved by Manager") && !c.Status.Contains("Rejected")).Sum(c => c.TotalAmount),

                AverageClaimAmount = allClaims.Any() ? allClaims.Average(c => c.TotalAmount) : 0,
                AverageHoursPerClaim = allClaims.Any() ? allClaims.Average(c => c.HoursWorked) : 0,
                AverageProcessingTime = CalculateAverageProcessingTime(allClaims),

                ClaimsByMonth = allClaims.GroupBy(c => c.Month).ToDictionary(g => g.Key, g => g.Count()),
                ClaimsByLecturer = allClaims.GroupBy(c => c.Lecturer?.FullName ?? "Unknown").ToDictionary(g => g.Key, g => g.Count()),

                ApprovalRate = allClaims.Count > 0 ? (double)allClaims.Count(c => c.Status.Contains("Approved")) / allClaims.Count * 100 : 0,
                RejectionRate = allClaims.Count > 0 ? (double)allClaims.Count(c => c.Status.Contains("Rejected")) / allClaims.Count * 100 : 0,

                GeneratedAt = DateTime.Now
            };

            _logger.LogInformation($"Statistics generated: {statistics.TotalClaims} claims, R{statistics.TotalAmountApproved:N2} approved");

            return statistics;
        }

        /// <summary>
        /// Simulates notification to stakeholders (email, SMS, etc.)
        /// In production, this would integrate with actual notification services
        /// </summary>
        public async Task NotifyStakeholdersAsync(Claim claim, string action)
        {
            try
            {
                var lecturer = await _context.Lecturers.FindAsync(claim.LecturerID);

                // Log notification (in production, send actual email/SMS)
                var message = action.ToLower() switch
                {
                    "auto-approved" => $"Your claim #{claim.ClaimID} for {claim.Month} (R{claim.TotalAmount:N2}) has been automatically approved.",
                    "auto-rejected" => $"Your claim #{claim.ClaimID} for {claim.Month} has been rejected. Reason: {claim.RejectionReason}",
                    "approved" => $"Your claim #{claim.ClaimID} for {claim.Month} (R{claim.TotalAmount:N2}) has been approved for payment.",
                    "rejected" => $"Your claim #{claim.ClaimID} for {claim.Month} has been rejected. Please review and resubmit if necessary.",
                    _ => $"Status update for claim #{claim.ClaimID}: {claim.Status}"
                };

                _logger.LogInformation($"Notification sent to {lecturer?.Email}: {message}");

                // In production, implement actual notification:
                // await _emailService.SendEmailAsync(lecturer.Email, "Claim Status Update", message);
                // await _smsService.SendSmsAsync(lecturer.PhoneNumber, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification for claim {claim.ClaimID}");
            }

            await Task.CompletedTask;
        }

        private double CalculateAverageProcessingTime(List<Claim> claims)
        {
            var processedClaims = claims.Where(c => c.Status != "Submitted" && c.Status != "Pending").ToList();

            if (!processedClaims.Any())
                return 0;

            // In a real system, you'd track status change timestamps
            // For now, return a mock value
            return 2.5; // Average days
        }
    }

    #region Data Models for Automation

    public class ClaimValidationResult
    {
        public int ClaimId { get; set; }
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public bool CanAutoApprove { get; set; }
        public string? ActionTaken { get; set; }
    }

    public class ClaimStatistics
    {
        public int TotalClaims { get; set; }
        public int SubmittedClaims { get; set; }
        public int ApprovedByCoordinator { get; set; }
        public int ApprovedByManager { get; set; }
        public int RejectedClaims { get; set; }

        public decimal TotalAmountClaimed { get; set; }
        public decimal TotalAmountApproved { get; set; }
        public decimal TotalAmountPending { get; set; }

        public decimal AverageClaimAmount { get; set; }
        public double AverageHoursPerClaim { get; set; }
        public double AverageProcessingTime { get; set; }

        public Dictionary<string, int> ClaimsByMonth { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ClaimsByLecturer { get; set; } = new Dictionary<string, int>();

        public double ApprovalRate { get; set; }
        public double RejectionRate { get; set; }

        public DateTime GeneratedAt { get; set; }
    }

    #endregion
}