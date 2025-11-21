using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMCS_App.Models
{
    public class ProgrammeCoordinator
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CoordinatorID { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Password { get; set; }

        public void ApproveClaim(Claim claim)
        {
            claim.UpdateStatus("Approved by Coordinator");
        }

        public void RejectClaim(Claim claim, string reason)
        {
            claim.UpdateStatus("Rejected by Coordinator", reason);
        }
    }
}