using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data.Models;

public class UserSession
{
    public int UserSessionId { get; set; }

    [Required, MaxLength(64)]
    public string SessionGuid { get; set; } = "";   // store guid string

    [Required]
    public string UserId { get; set; } = "";        // AspNetUsers.Id (string)

    public int CompanyId { get; set; }

    [MaxLength(256)]
    public string UserName { get; set; } = "";

    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresOnUtc { get; set; } = DateTime.UtcNow.AddDays(7);

    public bool IsActive { get; set; } = true;
}
