using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS_App.Models
{
    [Table("Claims")]
    public class Claim
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ClaimID { get; set; }

        [Required]
        [ForeignKey("Lecturer")]
        public int LecturerID { get; set; }

        [StringLength(100)]
        public string? ModuleName { get; set; }

        [Required]
        [StringLength(50)]
        public string Month { get; set; } = string.Empty;

        [Required]
        [Range(1, 200, ErrorMessage = "Hours worked must be between 1 and 200")]
        public int HoursWorked { get; set; }

        [Required]
        [Range(0.01, 1000, ErrorMessage = "Hourly rate must be between 0.01 and 1000")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [StringLength(100)]
        public string Status { get; set; } = "Submitted";

        [Required]
        public DateTime SubmissionDate { get; set; } = DateTime.Now;

        public string? SupportingDocument { get; set; }

        public string? RejectionReason { get; set; }

        // Navigation property
        public virtual Lecturer? Lecturer { get; set; }

        public decimal CalculateTotal()
        {
            TotalAmount = HoursWorked * HourlyRate;
            return TotalAmount;
        }

        public void SubmitForApproval()
        {
            Status = "Submitted";
        }

        public void UpdateStatus(string newStatus)
        {
            Status = newStatus;
        }

        public void UpdateStatus(string newStatus, string rejectionReason)
        {
            Status = newStatus;
            RejectionReason = rejectionReason;
        }
    }
}