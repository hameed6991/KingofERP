namespace UaeEInvoice.Data;

public class ChequeTransaction
{
    public int ChequeTransactionId { get; set; }
    public int CompanyId { get; set; }

    public ChequeDirection Direction { get; set; }
    public ChequeType Type { get; set; }
    public ChequeStatus Status { get; set; } = ChequeStatus.Draft;

    public int ChequeBookId { get; set; }
    public string ChequeNo { get; set; } = "";

    public DateTime IssueDate { get; set; } = DateTime.Today;      // created date
    public DateTime ChequeDate { get; set; } = DateTime.Today;     // printed date on cheque (PDC future)
    public DateTime? ClearDate { get; set; }                       // when bank clears

    public decimal Amount { get; set; }

    // Party
    public int? CustomerId { get; set; }
    public int? VendorId { get; set; }
    public string PayeeName { get; set; } = "";

    // Link to source documents (optional but recommended)
    public int? InvoiceId { get; set; }              // incoming
    public int? ReceiptId { get; set; }
    public int? PurchaseInvoiceId { get; set; }      // outgoing
    public int? PurchasePaymentId { get; set; }

    // Accounting
    public int BankAccountNo { get; set; }           // bank GL account
    public int ClearingAccountNo { get; set; }       // PDC clearing / cheques-in-hand
    public int CounterAccountNo { get; set; }        // AR/AP/Expense based on transaction

    // Audit
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public string? Notes { get; set; }
}
