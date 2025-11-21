using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS_App.Models
{
    [Table("Lecturers")]
    public class Lecturer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LecturerID { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Password { get; set; }

        [Required]
        [StringLength(100)]
        public string ModuleName { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; }

        public void SubmitClaim(Claim claim)
        {
            claim.SubmitForApproval();
        }

        public void UploadSupportingDocument(Claim claim, string filePath)
        {
            claim.SupportingDocument = filePath;
        }

        public string TrackClaimStatus(Claim claim)
        {
            return claim.Status;
        }
    }
}