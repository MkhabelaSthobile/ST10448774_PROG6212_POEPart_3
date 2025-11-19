using CMCS_App.Data;
using CMCS_App.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CMCS_App.Services
{
    public interface IHRReportService
    {
        Task<string> GenerateInvoiceReportAsync(int? lecturerId = null, string? month = null);
        Task<string> GeneratePaymentSummaryAsync(string month);
        Task<byte[]> ExportToCSVAsync(string reportType, Dictionary<string, string> filters);
        Task<LecturerPerformanceReport> GenerateLecturerPerformanceReportAsync(int lecturerId);
        Task<MonthlyFinancialReport> GenerateMonthlyFinancialReportAsync(string month);
    }

    public class HRReportService : IHRReportService
    {
        private readonly ApplicationDbContext _context;

        public HRReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateInvoiceReportAsync(int? lecturerId = null, string? month = null)
        {
            var query = _context.Claims
                .Include(c => c.Lecturer)
                .Where(c => c.Status == "Approved by Manager");

            if (lecturerId.HasValue)
                query = query.Where(c => c.LecturerID == lecturerId.Value);

            if (!string.IsNullOrEmpty(month))
                query = query.Where(c => c.Month == month);

            var claims = await query.OrderBy(c => c.SubmissionDate).ToListAsync();

            var report = new StringBuilder();
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine("PAYMENT INVOICE REPORT");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine();

            if (lecturerId.HasValue)
            {
                var lecturer = await _context.Lecturers.FindAsync(lecturerId.Value);
                report.AppendLine($"Lecturer: {lecturer?.FullName}");
                report.AppendLine($"Email: {lecturer?.Email}");
                report.AppendLine();
            }

            if (!string.IsNullOrEmpty(month))
            {
                report.AppendLine($"Month: {month}");
                report.AppendLine();
            }

            report.AppendLine($"{"Claim ID",-10} {"Lecturer",-25} {"Month",-15} {"Hours",-10} {"Rate",-15} {"Total",-15}");
            report.AppendLine("-".PadRight(90, '-'));

            decimal grandTotal = 0;
            foreach (var claim in claims)
            {
                report.AppendLine($"#{claim.ClaimID,-9} {claim.Lecturer?.FullName,-25} {claim.Month,-15} {claim.HoursWorked,-10} R{claim.HourlyRate,-13:N2} R{claim.TotalAmount,-13:N2}");
                grandTotal += claim.TotalAmount;
            }

            report.AppendLine("-".PadRight(90, '-'));
            report.AppendLine($"{"TOTAL PAYMENT DUE:",-75} R{grandTotal,-13:N2}");
            report.AppendLine("=".PadRight(80, '='));

            return report.ToString();
        }

        public async Task<string> GeneratePaymentSummaryAsync(string month)
        {
            var claims = await _context.Claims
                .Include(c => c.Lecturer)
                .Where(c => c.Month == month)
                .ToListAsync();

            var report = new StringBuilder();
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine($"MONTHLY PAYMENT SUMMARY - {month.ToUpper()}");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine();

            // Summary by status
            report.AppendLine("CLAIM STATUS BREAKDOWN:");
            report.AppendLine("-".PadRight(50, '-'));
            report.AppendLine($"Submitted/Pending:        {claims.Count(c => c.Status == "Submitted" || c.Status == "Pending"),-5} claims");
            report.AppendLine($"Approved by Coordinator:  {claims.Count(c => c.Status == "Approved by Coordinator"),-5} claims");
            report.AppendLine($"Approved by Manager:      {claims.Count(c => c.Status == "Approved by Manager"),-5} claims");
            report.AppendLine($"Rejected:                 {claims.Count(c => c.Status.Contains("Rejected")),-5} claims");
            report.AppendLine($"TOTAL:                    {claims.Count,-5} claims");
            report.AppendLine();

            // Financial summary
            report.AppendLine("FINANCIAL SUMMARY:");
            report.AppendLine("-".PadRight(50, '-'));
            report.AppendLine($"Total Claimed:            R{claims.Sum(c => c.TotalAmount),15:N2}");
            report.AppendLine($"Approved for Payment:     R{claims.Where(c => c.Status == "Approved by Manager").Sum(c => c.TotalAmount),15:N2}");
            report.AppendLine($"Pending Approval:         R{claims.Where(c => !c.Status.Contains("Approved by Manager") && !c.Status.Contains("Rejected")).Sum(c => c.TotalAmount),15:N2}");
            report.AppendLine($"Rejected Amount:          R{claims.Where(c => c.Status.Contains("Rejected")).Sum(c => c.TotalAmount),15:N2}");
            report.AppendLine();

            // Lecturer breakdown
            report.AppendLine("PAYMENT BY LECTURER:");
            report.AppendLine("-".PadRight(70, '-'));
            report.AppendLine($"{"Lecturer",-30} {"Claims",-10} {"Total Hours",-15} {"Amount",-15}");
            report.AppendLine("-".PadRight(70, '-'));

            var approvedClaims = claims.Where(c => c.Status == "Approved by Manager").ToList();
            var lecturerGroups = approvedClaims.GroupBy(c => c.Lecturer?.FullName ?? "Unknown");

            foreach (var group in lecturerGroups.OrderByDescending(g => g.Sum(c => c.TotalAmount)))
            {
                var lecturerClaims = group.ToList();
                report.AppendLine($"{group.Key,-30} {lecturerClaims.Count,-10} {lecturerClaims.Sum(c => c.HoursWorked),-15} R{lecturerClaims.Sum(c => c.TotalAmount),-13:N2}");
            }

            report.AppendLine("=".PadRight(80, '='));

            return report.ToString();
        }

        // Exports data to CSV format
        public async Task<byte[]> ExportToCSVAsync(string reportType, Dictionary<string, string> filters)
        {
            var csv = new StringBuilder();

            switch (reportType.ToLower())
            {
                case "claims":
                    csv.AppendLine("Claim ID,Lecturer,Email,Module,Month,Hours Worked,Hourly Rate,Total Amount,Status,Submission Date,Rejection Reason");

                    var claims = await _context.Claims.Include(c => c.Lecturer).ToListAsync();
                    foreach (var claim in claims)
                    {
                        csv.AppendLine($"{claim.ClaimID}," +
                            $"\"{claim.Lecturer?.FullName ?? "N/A"}\"," +
                            $"\"{claim.Lecturer?.Email ?? "N/A"}\"," +
                            $"\"{claim.ModuleName ?? "N/A"}\"," +
                            $"\"{claim.Month}\"," +
                            $"{claim.HoursWorked}," +
                            $"{claim.HourlyRate:F2}," +
                            $"{claim.TotalAmount:F2}," +
                            $"\"{claim.Status}\"," +
                            $"{claim.SubmissionDate:yyyy-MM-dd HH:mm}," +
                            $"\"{claim.RejectionReason ?? "N/A"}\"");
                    }
                    break;

                case "lecturers":
                    csv.AppendLine("Lecturer ID,Full Name,Email,Module,Hourly Rate,Total Claims,Total Amount Earned,Average Monthly Hours");

                    var lecturers = await _context.Lecturers.ToListAsync();
                    foreach (var lecturer in lecturers)
                    {
                        var lecturerClaims = await _context.Claims
                            .Where(c => c.LecturerID == lecturer.LecturerID && c.Status == "Approved by Manager")
                            .ToListAsync();

                        csv.AppendLine($"{lecturer.LecturerID}," +
                            $"\"{lecturer.FullName}\"," +
                            $"\"{lecturer.Email}\"," +
                            $"\"{lecturer.ModuleName}\"," +
                            $"{lecturer.HourlyRate:F2}," +
                            $"{lecturerClaims.Count}," +
                            $"{lecturerClaims.Sum(c => c.TotalAmount):F2}," +
                            $"{(lecturerClaims.Any() ? lecturerClaims.Average(c => c.HoursWorked) : 0):F2}");
                    }
                    break;

                case "financial":
                    csv.AppendLine("Month,Total Claims,Submitted,Approved,Rejected,Total Amount,Approved Amount,Average Claim Value");

                    var allClaims = await _context.Claims.ToListAsync();
                    var monthlyData = allClaims.GroupBy(c => c.Month);

                    foreach (var month in monthlyData.OrderBy(m => m.Key))
                    {
                        var monthClaims = month.ToList();
                        csv.AppendLine($"\"{month.Key}\"," +
                            $"{monthClaims.Count}," +
                            $"{monthClaims.Count(c => c.Status == "Submitted" || c.Status == "Pending")}," +
                            $"{monthClaims.Count(c => c.Status.Contains("Approved"))}," +
                            $"{monthClaims.Count(c => c.Status.Contains("Rejected"))}," +
                            $"{monthClaims.Sum(c => c.TotalAmount):F2}," +
                            $"{monthClaims.Where(c => c.Status == "Approved by Manager").Sum(c => c.TotalAmount):F2}," +
                            $"{(monthClaims.Any() ? monthClaims.Average(c => c.TotalAmount) : 0):F2}");
                    }
                    break;

                default:
                    csv.AppendLine("Invalid report type");
                    break;
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        /// <summary>
        /// Generates performance report for a specific lecturer
        /// </summary>
        public async Task<LecturerPerformanceReport> GenerateLecturerPerformanceReportAsync(int lecturerId)
        {
            var lecturer = await _context.Lecturers.FindAsync(lecturerId);
            if (lecturer == null)
                throw new ArgumentException("Lecturer not found");

            var claims = await _context.Claims
                .Where(c => c.LecturerID == lecturerId)
                .ToListAsync();

            var report = new LecturerPerformanceReport
            {
                LecturerId = lecturerId,
                LecturerName = lecturer.FullName,
                Email = lecturer.Email,
                ModuleName = lecturer.ModuleName,
                HourlyRate = lecturer.HourlyRate,

                TotalClaims = claims.Count,
                ApprovedClaims = claims.Count(c => c.Status.Contains("Approved")),
                RejectedClaims = claims.Count(c => c.Status.Contains("Rejected")),
                PendingClaims = claims.Count(c => c.Status == "Submitted" || c.Status == "Pending"),

                TotalHoursWorked = claims.Where(c => c.Status == "Approved by Manager").Sum(c => c.HoursWorked),
                TotalEarnings = claims.Where(c => c.Status == "Approved by Manager").Sum(c => c.TotalAmount),
                AverageMonthlyHours = claims.Any() ? claims.Average(c => c.HoursWorked) : 0,
                AverageClaimAmount = claims.Any() ? claims.Average(c => c.TotalAmount) : 0,

                ApprovalRate = claims.Count > 0 ? (double)claims.Count(c => c.Status.Contains("Approved")) / claims.Count * 100 : 0,
                RejectionRate = claims.Count > 0 ? (double)claims.Count(c => c.Status.Contains("Rejected")) / claims.Count * 100 : 0,

                ClaimsByMonth = claims.GroupBy(c => c.Month).ToDictionary(g => g.Key, g => g.Count()),
                EarningsByMonth = claims.Where(c => c.Status == "Approved by Manager")
                    .GroupBy(c => c.Month)
                    .ToDictionary(g => g.Key, g => g.Sum(c => c.TotalAmount)),

                LastClaimDate = claims.Any() ? claims.Max(c => c.SubmissionDate) : null,
                GeneratedAt = DateTime.Now
            };

            return report;
        }

        /// <summary>
        /// Generates comprehensive monthly financial report
        /// </summary>
        public async Task<MonthlyFinancialReport> GenerateMonthlyFinancialReportAsync(string month)
        {
            var claims = await _context.Claims
                .Include(c => c.Lecturer)
                .Where(c => c.Month == month)
                .ToListAsync();

            var report = new MonthlyFinancialReport
            {
                Month = month,

                TotalClaims = claims.Count,
                SubmittedClaims = claims.Count(c => c.Status == "Submitted" || c.Status == "Pending"),
                ApprovedClaims = claims.Count(c => c.Status == "Approved by Manager"),
                RejectedClaims = claims.Count(c => c.Status.Contains("Rejected")),

                TotalAmountClaimed = claims.Sum(c => c.TotalAmount),
                ApprovedAmount = claims.Where(c => c.Status == "Approved by Manager").Sum(c => c.TotalAmount),
                PendingAmount = claims.Where(c => !c.Status.Contains("Approved by Manager") && !c.Status.Contains("Rejected")).Sum(c => c.TotalAmount),
                RejectedAmount = claims.Where(c => c.Status.Contains("Rejected")).Sum(c => c.TotalAmount),

                TotalHoursWorked = claims.Where(c => c.Status == "Approved by Manager").Sum(c => c.HoursWorked),
                UniqueLecturers = claims.Select(c => c.LecturerID).Distinct().Count(),
                AverageClaimValue = claims.Any() ? claims.Average(c => c.TotalAmount) : 0,
                AverageHoursPerClaim = claims.Any() ? claims.Average(c => c.HoursWorked) : 0,

                PaymentByLecturer = claims.Where(c => c.Status == "Approved by Manager")
                    .GroupBy(c => new { c.Lecturer?.FullName, c.Lecturer?.Email })
                    .Select(g => new LecturerPaymentInfo
                    {
                        LecturerName = g.Key.FullName ?? "Unknown",
                        Email = g.Key.Email ?? "N/A",
                        ClaimCount = g.Count(),
                        TotalHours = g.Sum(c => c.HoursWorked),
                        TotalAmount = g.Sum(c => c.TotalAmount)
                    })
                    .OrderByDescending(l => l.TotalAmount)
                    .ToList(),

                GeneratedAt = DateTime.Now
            };

            return report;
        }
    }

    #region Report Data Models

    public class LecturerPerformanceReport
    {
        public int LecturerId { get; set; }
        public string LecturerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public decimal HourlyRate { get; set; }

        public int TotalClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public int RejectedClaims { get; set; }
        public int PendingClaims { get; set; }

        public int TotalHoursWorked { get; set; }
        public decimal TotalEarnings { get; set; }
        public double AverageMonthlyHours { get; set; }
        public decimal AverageClaimAmount { get; set; }

        public double ApprovalRate { get; set; }
        public double RejectionRate { get; set; }

        public Dictionary<string, int> ClaimsByMonth { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, decimal> EarningsByMonth { get; set; } = new Dictionary<string, decimal>();

        public DateTime? LastClaimDate { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class MonthlyFinancialReport
    {
        public string Month { get; set; } = string.Empty;

        public int TotalClaims { get; set; }
        public int SubmittedClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public int RejectedClaims { get; set; }

        public decimal TotalAmountClaimed { get; set; }
        public decimal ApprovedAmount { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal RejectedAmount { get; set; }

        public int TotalHoursWorked { get; set; }
        public int UniqueLecturers { get; set; }
        public decimal AverageClaimValue { get; set; }
        public double AverageHoursPerClaim { get; set; }

        public List<LecturerPaymentInfo> PaymentByLecturer { get; set; } = new List<LecturerPaymentInfo>();

        public DateTime GeneratedAt { get; set; }
    }

    public class LecturerPaymentInfo
    {
        public string LecturerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int ClaimCount { get; set; }
        public int TotalHours { get; set; }
        public decimal TotalAmount { get; set; }
    }

    #endregion
}