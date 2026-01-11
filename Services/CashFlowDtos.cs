namespace UaeEInvoice.Services;

public record CashFlowLine(string Name, decimal Amount);

public class CashFlowReport
{
    // Main values
    public decimal NetIncome { get; set; }

    public List<CashFlowLine> NonCashAdjustments { get; set; } = new();
    public List<CashFlowLine> WorkingCapitalChanges { get; set; } = new();

    public decimal OperatingCashFlow { get; set; }

    public List<CashFlowLine> InvestingActivities { get; set; } = new();
    public decimal InvestingCashFlow { get; set; }

    public List<CashFlowLine> FinancingActivities { get; set; } = new();
    public decimal FinancingCashFlow { get; set; }

    public decimal NetCashFlow { get; set; }

    // ✅ Diagnostics (to instantly know why 0.00)
    public int PeriodGlRows { get; set; }
    public int IncomeStatementAccounts { get; set; }
    public int CashAccountsTagged { get; set; }
    public int WorkingCapitalTagged { get; set; }
    public int InvestingTagged { get; set; }
    public int FinancingTagged { get; set; }
    public int NonCashTagged { get; set; }

    public bool LooksUntagged =>
        CashAccountsTagged == 0 && WorkingCapitalTagged == 0 && InvestingTagged == 0 && FinancingTagged == 0 && NonCashTagged == 0;
}
