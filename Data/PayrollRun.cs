using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class PayrollRun
{
    public int PayrollRunId { get; set; }

    public int CompanyId { get; set; }

    // First day of month (ex: 2025-12-01)
    public DateTime PeriodMonth { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    [Required, StringLength(30)]
    public string RunNo { get; set; } = "";   // AT-PAY-000001

    [Required, StringLength(20)]
    public string Status { get; set; } = "Draft"; // Draft / Approved / Paid

    public DateTime? ApprovedOn { get; set; }
    public DateTime? PaidOn { get; set; }


    public DateTime CreatedOn { get; set; } = DateTime.Now;

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<PayrollLine> Lines { get; set; } = new();
}
