using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class Employee
{
    public int EmployeeId { get; set; }

    public int CompanyId { get; set; }

    [Required, StringLength(30)]
    public string EmpCode { get; set; } = "";     // EMP-000001

    [Required, StringLength(150)]
    public string EmpName { get; set; } = "";

    public DateTime JoinDate { get; set; } = DateTime.Today;

    [Range(0, 999999999)]
    public decimal BasicSalary { get; set; } = 0;

    [Range(0, 999999999)]
    public decimal Allowance { get; set; } = 0;

    [StringLength(20)]
    public string PaymentMethod { get; set; } = "Bank";   // Bank / Cash

    [StringLength(80)]
    public string? BankName { get; set; }

    [StringLength(34)]
    public string? IBAN { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string? Notes { get; set; }


}
