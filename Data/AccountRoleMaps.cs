using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class AccountRoleMap
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    [MaxLength(30)]
    public string RoleKey { get; set; } = "";   // CASH / BANK / AR / AP

    public int AccountNo { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
