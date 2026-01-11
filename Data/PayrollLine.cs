using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UaeEInvoice.Data;

public class PayrollLine
{
    public int PayrollLineId { get; set; }

    public int CompanyId { get; set; }

    public int PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }   // ✅ fixes: "PayrollLine does not contain Employee"

    [StringLength(30)]
    public string EmpCode { get; set; } = "";

    [StringLength(150)]
    public string EmpName { get; set; } = "";

    [Range(0, 999999999)]
    public decimal BasicSalary { get; set; }

    [Range(0, 999999999)]
    public decimal Allowance { get; set; }

    // ✅ main field (use this in DB)
    [Range(0, 999999999)]
    public decimal Deduction { get; set; }

    // ✅ compatibility (if your UI still uses "Deductions")
    [NotMapped]
    public decimal Deductions
    {
        get => Deduction;
        set => Deduction = value;
    }

    [StringLength(500)]
    public string? Notes { get; set; }

    [NotMapped]
    public decimal NetPay => BasicSalary + Allowance - Deduction;
}
