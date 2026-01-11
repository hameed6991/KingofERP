using Microsoft.AspNetCore.Identity;

namespace UaeEInvoice.Data;

public class ApplicationUser : IdentityUser
{
    public int CompanyId { get; set; }  // optional: multi-company data access
}
