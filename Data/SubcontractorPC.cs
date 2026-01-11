using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class SubcontractorPC
    {
        public int SubcontractorPCId { get; set; }
        public int CompanyId { get; set; }

        // Project + Subcontractor
        public int ConstructionProjectId { get; set; }
        public ConstructionProject? Project { get; set; }

        public int ConstructionSubcontractorId { get; set; }
        public ConstructionSubcontractor? Subcontractor { get; set; }

        // Document identity
        [Required, MaxLength(30)]
        public string PCNo { get; set; } = ""; // e.g. PC-0006

        // Period: store month start (ex: 2025-01-01)
        public DateTime PeriodMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

        public DateTime IssueDate { get; set; } = DateTime.Today;
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);

        // Rates
        [Range(0, 1)]
        public decimal VatRate { get; set; } = 0.05m;

        [Range(0, 1)]
        public decimal RetentionRate { get; set; } = 0.10m;

        // Inputs / computed numbers
        public decimal PreviousCumulative { get; set; } = 0m;      // previous certified/work done to date
        public decimal BackchargeThisMonth { get; set; } = 0m;     // deductions

        // Computed snapshots (stored for reporting & posting)
        public decimal WorkDoneToDate { get; set; } = 0m;          // sum of line totals
        public decimal ThisMonthGross { get; set; } = 0m;          // WorkDoneToDate - PreviousCumulative
        public decimal RetentionThisMonth { get; set; } = 0m;      // ThisMonthGross * RetentionRate
        public decimal PayableExVat { get; set; } = 0m;            // ThisMonthGross - RetentionThisMonth - BackchargeThisMonth
        public decimal VatAmount { get; set; } = 0m;               // PayableExVat * VatRate
        public decimal NetPayable { get; set; } = 0m;              // PayableExVat + VatAmount

        [MaxLength(20)]
        public string Status { get; set; } = "Draft"; // Draft / Approved / Posted

        public bool IsPosted { get; set; } = false;
        public DateTime? PostedOn { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public List<SubcontractorPCLine> Lines { get; set; } = new();
    }
}
