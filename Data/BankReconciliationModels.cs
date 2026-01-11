using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public enum BankTxnDirection { Debit = 0, Credit = 1 }
public enum BankLineStatus { Unmatched = 0, Suggested = 1, Reconciled = 2, Ignored = 3 }
public enum BankMatchMethod { None = 0, Reference = 1, AmountDate = 2, Manual = 3, AutoPosted = 4 }

public class BankAccount
{
    public int BankAccountId { get; set; }
    public int CompanyId { get; set; }

    [Required, MaxLength(120)]
    public string BankName { get; set; } = "";

    [Required, MaxLength(40)]
    public string AccountNo { get; set; } = "";

    [MaxLength(34)]
    public string? IBAN { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "AED";

    // ✅ GL Mapping (this is the BANK GL account in ChartOfAccounts)
    public int? GlAccountNo { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class BankStatementImport
{
    public int BankStatementImportId { get; set; }
    public int CompanyId { get; set; }
    public int BankAccountId { get; set; }

    [MaxLength(200)]
    public string SourceFileName { get; set; } = "";

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public List<BankStatementLine> Lines { get; set; } = new();
}

public class BankStatementLine
{
    public int BankStatementLineId { get; set; }
    public int CompanyId { get; set; }

    public int BankAccountId { get; set; }
    public int BankStatementImportId { get; set; }

    public DateTime TxnDate { get; set; }

    [MaxLength(500)]
    public string Narration { get; set; } = "";

    public BankTxnDirection Direction { get; set; }

    public decimal Amount { get; set; }          // always positive
    public decimal? RunningBalance { get; set; }

    [MaxLength(60)]
    public string? ExtractedRef { get; set; }

    public BankLineStatus Status { get; set; } = BankLineStatus.Unmatched;

    [MaxLength(20)]
    public string? MatchedVoucherType { get; set; }

    [MaxLength(30)]
    public string? MatchedVoucherNo { get; set; }

    public int? MatchedRefId { get; set; }

    public decimal? Confidence { get; set; }
    public BankMatchMethod MatchMethod { get; set; } = BankMatchMethod.None;

    [MaxLength(300)]
    public string? MatchNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
