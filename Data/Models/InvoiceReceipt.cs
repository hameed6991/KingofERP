using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class InvoiceReceipt
{
    public int InvoiceReceiptId { get; set; }

    [Required]
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [Required]
    public DateTime ReceiptDate { get; set; } = DateTime.Today;

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [StringLength(30)]
    public string Method { get; set; } = "Cash";

    [StringLength(100)]
    public string? ReferenceNo { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
