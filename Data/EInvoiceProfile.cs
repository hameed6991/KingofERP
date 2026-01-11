using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class EInvoiceProfile
{
    public int EInvoiceProfileId { get; set; }
    public int CompanyId { get; set; }

    [MaxLength(40)]
    public string Profile { get; set; } = "PINT-AE";

    public bool IsEnabled { get; set; } = true;

    // Future: ASP config (keep empty for now)
    [MaxLength(60)] public string? ProviderName { get; set; }
    [MaxLength(200)] public string? ApiBaseUrl { get; set; }
    [MaxLength(200)] public string? ApiKeyEncrypted { get; set; }
}
