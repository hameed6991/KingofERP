using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public class CashFlowService
{
    private readonly AppDbContext _db;
    public CashFlowService(AppDbContext db) => _db = db;

    public async Task<CashFlowReport> GetIndirectCashFlowAsync(int companyId, DateTime fromDate, DateTime toDateExclusive)
    {
        // Load COA
        var accounts = await _db.ChartOfAccounts.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.IsActive)
            .ToListAsync();

        var accByNo = accounts.ToDictionary(a => a.AccountNo, a => a);

        bool IsFS(ChartOfAccount a, string fs) =>
            string.Equals(a.FinancialStatement?.Trim(), fs, StringComparison.OrdinalIgnoreCase);

        bool IsType(ChartOfAccount a, params string[] types)
        {
            var t = (a.AccountType ?? "").Trim();
            foreach (var x in types)
                if (string.Equals(t, x, StringComparison.OrdinalIgnoreCase)) return true;

            // fallback keywords
            var tl = t.ToLowerInvariant();
            if (types.Any(z => z.Equals("Income", StringComparison.OrdinalIgnoreCase)) &&
                (tl.Contains("income") || tl.Contains("revenue") || tl.Contains("sales"))) return true;

            if (types.Any(z => z.Equals("Expense", StringComparison.OrdinalIgnoreCase)) &&
                (tl.Contains("expense") || tl.Contains("cost") || tl.Contains("cogs"))) return true;

            if (types.Any(z => z.Equals("Asset", StringComparison.OrdinalIgnoreCase)) && tl.Contains("asset")) return true;
            if (types.Any(z => z.Equals("Liability", StringComparison.OrdinalIgnoreCase)) && tl.Contains("liab")) return true;

            return false;
        }

        // Period GL rows
        var glPeriodQuery = _db.GeneralLedgerEntries.AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.TxnDate >= fromDate && g.TxnDate < toDateExclusive);

        var periodGlRows = await glPeriodQuery.CountAsync();

        // Debit totals per account (EF equivalent of your SQL)
        var debitTotals = await glPeriodQuery
            .GroupBy(g => g.DebitAccountNo)
            .Select(x => new { AccountNo = x.Key, Amount = x.Sum(v => v.Amount) })
            .ToDictionaryAsync(x => x.AccountNo, x => x.Amount);

        // Credit totals per account
        var creditTotals = await glPeriodQuery
            .GroupBy(g => g.CreditAccountNo)
            .Select(x => new { AccountNo = x.Key, Amount = x.Sum(v => v.Amount) })
            .ToDictionaryAsync(x => x.AccountNo, x => x.Amount);

        decimal Dr(int accNo) => debitTotals.TryGetValue(accNo, out var v) ? v : 0m;
        decimal Cr(int accNo) => creditTotals.TryGetValue(accNo, out var v) ? v : 0m;

        // ---------- 1) Net Income ----------
        var incomeAccounts = accounts.Where(a => IsFS(a, "IncomeStatement")).ToList();
        decimal netIncome = 0m;

        foreach (var a in incomeAccounts)
        {
            var dr = Dr(a.AccountNo);
            var cr = Cr(a.AccountNo);

            // Income increases on Credit; Expense increases on Debit
            if (IsType(a, "Income"))
                netIncome += (cr - dr);
            else if (IsType(a, "Expense"))
                netIncome -= (dr - cr);
        }

        // ---------- 2) Non-cash adjustments (Depreciation etc) ----------
        var nonCashLines = new List<CashFlowLine>();
        decimal nonCashTotal = 0m;

        foreach (var a in accounts.Where(x => x.IsNonCashExpense))
        {
            var amt = (Dr(a.AccountNo) - Cr(a.AccountNo)); // expense-like addback
            if (amt != 0)
            {
                nonCashLines.Add(new CashFlowLine(a.AccountName, amt));
                nonCashTotal += amt;
            }
        }

        // ---------- 3) Working capital changes (Opening vs Closing) ----------
        var wcNos = accounts.Where(a => a.IsWorkingCapital).Select(a => a.AccountNo).ToHashSet();

        async Task<Dictionary<int, (decimal Dr, decimal Cr)>> AggUntilAsync(DateTime cutOff)
        {
            var gl = _db.GeneralLedgerEntries.AsNoTracking()
                .Where(g => g.CompanyId == companyId && g.TxnDate < cutOff);

            var deb = await gl.GroupBy(g => g.DebitAccountNo)
                .Select(x => new { AccountNo = x.Key, Dr = x.Sum(v => v.Amount) })
                .ToListAsync();

            var cre = await gl.GroupBy(g => g.CreditAccountNo)
                .Select(x => new { AccountNo = x.Key, Cr = x.Sum(v => v.Amount) })
                .ToListAsync();

            var dict = new Dictionary<int, (decimal Dr, decimal Cr)>();
            foreach (var d in deb)
            {
                dict.TryGetValue(d.AccountNo, out var cur);
                dict[d.AccountNo] = (cur.Dr + d.Dr, cur.Cr);
            }
            foreach (var c in cre)
            {
                dict.TryGetValue(c.AccountNo, out var cur);
                dict[c.AccountNo] = (cur.Dr, cur.Cr + c.Cr);
            }
            return dict;
        }

        decimal NormalBalance(ChartOfAccount a, decimal dr, decimal cr)
        {
            // Asset/Expense => Dr - Cr; Liability/Equity/Income => Cr - Dr
            if (IsType(a, "Asset") || IsType(a, "Expense")) return dr - cr;
            return cr - dr;
        }

        var openAgg = await AggUntilAsync(fromDate);
        var closeAgg = await AggUntilAsync(toDateExclusive);

        var wcLines = new List<CashFlowLine>();
        decimal wcTotal = 0m;

        foreach (var accNo in wcNos)
        {
            if (!accByNo.TryGetValue(accNo, out var a)) continue;

            openAgg.TryGetValue(accNo, out var ob);
            closeAgg.TryGetValue(accNo, out var cb);

            var opening = NormalBalance(a, ob.Dr, ob.Cr);
            var closing = NormalBalance(a, cb.Dr, cb.Cr);
            var delta = closing - opening;

            // Indirect method:
            // Asset increase => subtract; Asset decrease => add
            // Liability increase => add; Liability decrease => subtract
            decimal adj;
            if (IsType(a, "Asset")) adj = -delta;
            else if (IsType(a, "Liability")) adj = +delta;
            else continue;

            if (adj != 0)
            {
                wcLines.Add(new CashFlowLine($"Change in {a.AccountName}", adj));
                wcTotal += adj;
            }
        }

        var operating = netIncome + nonCashTotal + wcTotal;

        // ---------- 4) Investing & Financing (based on cash movements) ----------
        var cashNos = accounts.Where(a => a.IsCashAccount).Select(a => a.AccountNo).ToHashSet();

        var glCash = await glPeriodQuery
            .Where(g => cashNos.Contains(g.DebitAccountNo) || cashNos.Contains(g.CreditAccountNo))
            .Select(g => new { g.DebitAccountNo, g.CreditAccountNo, g.Amount })
            .ToListAsync();

        var investingMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var financingMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        decimal investingTotal = 0m;
        decimal financingTotal = 0m;

        foreach (var g in glCash)
        {
            bool cashDebit = cashNos.Contains(g.DebitAccountNo);
            bool cashCredit = cashNos.Contains(g.CreditAccountNo);

            decimal cashEffect;
            int otherAcc;

            if (cashDebit)
            {
                cashEffect = +g.Amount;     // cash in
                otherAcc = g.CreditAccountNo;
            }
            else if (cashCredit)
            {
                cashEffect = -g.Amount;     // cash out
                otherAcc = g.DebitAccountNo;
            }
            else continue;

            if (!accByNo.TryGetValue(otherAcc, out var other)) continue;

            var grp = (other.CashFlowGroup ?? "").Trim();

            if (string.Equals(grp, "Investing", StringComparison.OrdinalIgnoreCase))
            {
                investingMap[other.AccountName] = investingMap.TryGetValue(other.AccountName, out var v) ? v + cashEffect : cashEffect;
                investingTotal += cashEffect;
            }
            else if (string.Equals(grp, "Financing", StringComparison.OrdinalIgnoreCase))
            {
                financingMap[other.AccountName] = financingMap.TryGetValue(other.AccountName, out var v) ? v + cashEffect : cashEffect;
                financingTotal += cashEffect;
            }
        }

        // Build report
        var report = new CashFlowReport
        {
            NetIncome = netIncome,
            NonCashAdjustments = nonCashLines.OrderByDescending(x => Math.Abs(x.Amount)).ToList(),
            WorkingCapitalChanges = wcLines.OrderByDescending(x => Math.Abs(x.Amount)).ToList(),
            OperatingCashFlow = operating,

            InvestingActivities = investingMap.Select(x => new CashFlowLine(x.Key, x.Value))
                .OrderByDescending(x => Math.Abs(x.Amount)).ToList(),
            InvestingCashFlow = investingTotal,

            FinancingActivities = financingMap.Select(x => new CashFlowLine(x.Key, x.Value))
                .OrderByDescending(x => Math.Abs(x.Amount)).ToList(),
            FinancingCashFlow = financingTotal,

            PeriodGlRows = periodGlRows,
            IncomeStatementAccounts = incomeAccounts.Count,
            CashAccountsTagged = cashNos.Count,
            WorkingCapitalTagged = wcNos.Count,
            InvestingTagged = accounts.Count(a => string.Equals((a.CashFlowGroup ?? "").Trim(), "Investing", StringComparison.OrdinalIgnoreCase)),
            FinancingTagged = accounts.Count(a => string.Equals((a.CashFlowGroup ?? "").Trim(), "Financing", StringComparison.OrdinalIgnoreCase)),
            NonCashTagged = accounts.Count(a => a.IsNonCashExpense)
        };

        report.NetCashFlow = report.OperatingCashFlow + report.InvestingCashFlow + report.FinancingCashFlow;
        return report;
    }
}
