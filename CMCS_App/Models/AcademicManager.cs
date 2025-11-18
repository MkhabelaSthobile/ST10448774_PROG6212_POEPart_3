using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS_App.Models
{
    public class AcademicManager
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ManagerID { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Email { get; set; } = string.Empty;

        public void VerifyClaim(Claim claim)
        {
            claim.UpdateStatus("Verified by Manager");
        }

        public void ApproveClaim(Claim claim)
        {
            claim.UpdateStatus("Approved by Manager");
        }

        public void RejectClaim(Claim claim, string reason)
        {
            claim.UpdateStatus("Rejected by Manager", reason);
        }

        public string GenerateSummaryReport(List<Claim> claims)
        {
            var approvedClaims = claims.Count(c => c.Status.Contains("Approved by Manager"));
            var rejectedClaims = claims.Count(c => c.Status.Contains("Rejected by Manager"));
            var totalAmount = claims.Where(c => c.Status.Contains("Approved by Manager")).Sum(c => c.TotalAmount);

            return $"Total Claims: {claims.Count}, " +
                   $"Approved: {approvedClaims}, " +
                   $"Rejected: {rejectedClaims}, " +
                   $"Total Amount: R{totalAmount:N2}";
        }

        public Dictionary<string, decimal> GetMonthlySummary(List<Claim> claims)
        {
            return claims
                .Where(c => c.Status.Contains("Approved by Manager"))
                .GroupBy(c => c.Month)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.TotalAmount));
        }
    }
}