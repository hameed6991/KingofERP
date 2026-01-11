using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class EInvoiceDocument
{
    public int EInvoiceDocumentId { get; set; }

    public int CompanyId { get; set; }

    // Source document
    [MaxLength(10)]
    public string SourceType { get; set; } = "INV"; // INV / PINV / CRN etc

    public int SourceId { get; set; }              // InvoiceId (or PurchaseInvoiceId)

    // Draft / Sent / Accepted / Rejected
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    // Profile / standard marker (for now draft)
    [MaxLength(40)]
    public string Profile { get; set; } = "PINT-AE-DRAFT";

    // Generated XML
    public string XmlPayload { get; set; } = "";

    [MaxLength(128)]
    public string? PayloadHashSha256 { get; set; }

    // Future: ASP integration fields
    [MaxLength(60)]
    public string? ProviderName { get; set; }

    [MaxLength(120)]
    public string? ProviderMessageId { get; set; }

    public string? ProviderRawResponse { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
