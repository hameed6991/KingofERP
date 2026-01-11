using System.ComponentModel.DataAnnotations;
using UaeEInvoice.Data;   // IMPORTANT: where PurchaseInvoice exists

namespace UaeEInvoice.Data;

public class PurchasePayment
{
    public int PurchasePaymentId { get; set; }

    [Required]
    public int PurchaseInvoiceId { get; set; }   // FK

    public PurchaseInvoice? PurchaseInvoice { get; set; }  // navigation (optional)

    [Required]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [StringLength(30)]
    public string Method { get; set; } = "Cash";

    [StringLength(100)]
    public string? ReferenceNo { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
