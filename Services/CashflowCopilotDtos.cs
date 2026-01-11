namespace UaeEInvoice.Services.CashFlowCopilot;

public class CashflowResponseDto
{
    public CashflowSummaryDto Summary { get; set; } = new();
    public List<CashflowRowDto> Rows { get; set; } = new();
    public List<CashflowAlertDto> Alerts { get; set; } = new();
}

public class CashflowSummaryDto
{
    public decimal OpeningCash { get; set; }

    public decimal ActualIn { get; set; }
    public decimal ActualOut { get; set; }

    public decimal ExpectedIn { get; set; }
    public decimal ExpectedOut { get; set; }

    public decimal TotalIn => ActualIn + ExpectedIn;
    public decimal TotalOut => ActualOut + ExpectedOut;

    public decimal WorstBalance { get; set; }
    public DateTime? WorstBalanceDate { get; set; }

    public int DeficitDaysNext7 { get; set; }
}

public class CashflowRowDto
{
    public DateTime Date { get; set; }

    public decimal ActualIn { get; set; }
    public decimal ActualOut { get; set; }

    public decimal ExpectedIn { get; set; }
    public decimal ExpectedOut { get; set; }

    public decimal InTotal => ActualIn + ExpectedIn;
    public decimal OutTotal => ActualOut + ExpectedOut;

    public decimal Net => InTotal - OutTotal;

    public decimal Running { get; set; }
    public string? Notes { get; set; }
}

public class CashflowAlertDto
{
    // "danger" / "warning" / "info" / "success"
    public string Severity { get; set; } = "info";

    public DateTime Date { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";

    // due date related chips
    public int? DaysLeft { get; set; }

    public decimal? Amount { get; set; }
}
