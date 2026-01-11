using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class GeneralLedgerEntry
{
    public int GeneralLedgerEntryId { get; set; }
    public int CompanyId { get; set; }

    public DateTime TxnDate { get; set; } = DateTime.Today;

    [MaxLength(20)]
    public string VoucherType { get; set; } = "";   // INV/REC/PINV/PPAY

    [MaxLength(30)]
    public string VoucherNo { get; set; } = "";     // InvoiceNo / PurchaseNo etc

    public int? RefId { get; set; }                 // InvoiceId / PurchaseInvoiceId etc

    // One row = one Debit and one Credit
    public int DebitAccountNo { get; set; }
    public int CreditAccountNo { get; set; }

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [MaxLength(300)]
    public string? Narration { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
