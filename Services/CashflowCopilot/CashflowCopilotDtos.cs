namespace UaeEInvoice.Services.CashflowCopilot;

public class CashflowResponseDto
{
    public int CompanyId { get; set; }

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public decimal OpeningCash { get; set; }

    // Totals for period
    public decimal TotalActualIn { get; set; }
    public decimal TotalExpectedIn { get; set; }

    public decimal TotalActualOut { get; set; }
    public decimal TotalExpectedOut { get; set; }

    public decimal WorstBalance { get; set; }
    public DateTime WorstBalanceDate { get; set; }

    public List<CashflowRowDto> Rows { get; set; } = new();
    public List<CashflowAlertDto> Alerts { get; set; } = new();
}

public class CashflowRowDto
{
    public DateTime Date { get; set; }

    public decimal ActualIn { get; set; }
    public decimal ExpectedIn { get; set; }

    public decimal ActualOut { get; set; }
    public decimal ExpectedOut { get; set; }

    public decimal Running { get; set; }

    public string? Notes { get; set; }
}

public class CashflowAlertDto
{
    public DateTime Date { get; set; }
    public string Severity { get; set; } = "info";   // danger / warning / success / info
    public string Message { get; set; } = "";
}

public sealed class RiskLineDto
{
    public string Title { get; set; } = "";          // e.g. "INV-00012"
    public string VoucherType { get; set; } = "";    // e.g. "INV"
    public string VoucherNo { get; set; } = "";      // e.g. "00012"
    public int? RefId { get; set; }                  // InvoiceId/PurchaseInvoiceId if stored
    public DateTime FirstTxnDate { get; set; }
    public int AgeDays { get; set; }
    public decimal Amount { get; set; }              // Outstanding amount
}

public sealed class RiskRadarDto
{
    public List<RiskLineDto> TopReceivables { get; set; } = new();
    public List<RiskLineDto> TopPayables { get; set; } = new();
    public List<RiskLineDto> TopExpenseCategories { get; set; } = new();
}
