using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class RecurringCashRule
{
    public int RecurringCashRuleId { get; set; }
    public int CompanyId { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = ""; // Rent/Salary/DEWA

    [Required, MaxLength(10)]
    public string Direction { get; set; } = "Out"; // In/Out

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [Required, MaxLength(12)]
    public string Frequency { get; set; } = "Monthly"; // Daily/Weekly/Monthly

    public DateTime NextDate { get; set; } = DateTime.Today;

    public bool IsActive { get; set; } = true;
}
