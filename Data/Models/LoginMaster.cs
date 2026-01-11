using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class LoginMaster
{
    public int LoginMasterId { get; set; }

    public int CompanyId { get; set; }

    [MaxLength(100)]
    public string UserName { get; set; } = "";

    [MaxLength(64)]
    public string SessionGuid { get; set; } = "";   // store GUID string

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenOn { get; set; }

    public DateTime ExpiresOn { get; set; } = DateTime.UtcNow.AddDays(7);

    public bool IsActive { get; set; } = true;

    [MaxLength(200)]
    public string? Device { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }
}
