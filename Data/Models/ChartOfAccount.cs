public class ChartOfAccount
{
    public int ChartOfAccountId { get; set; }
    public int CompanyId { get; set; }

    public int AccountNo { get; set; }
    public string AccountName { get; set; } = "";

    public string AccountType { get; set; } = "";            // Asset/Liability/Equity/Income/Expense
    public string FinancialStatement { get; set; } = "";     // BalanceSheet/IncomeStatement
    public bool IsActive { get; set; } = true;

    // ✅ NEW (cashflow)
    public bool IsCashAccount { get; set; }                  // true for Cash/Bank
    public bool IsWorkingCapital { get; set; }               // true for AR, AP, Inventory, VAT input/output if you want
    public bool IsNonCashExpense { get; set; }               // true for Depreciation etc

    public string CashFlowGroup { get; set; } = "Operating"; // Operating/Investing/Financing/None
}
