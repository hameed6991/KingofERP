using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Services.CashflowCopilot;

public class CashflowCopilotService
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;

    public CashflowCopilotService(AppDbContext db, ICurrentCompany currentCompany)
    {
        _db = db;
        _currentCompany = currentCompany;
    }

    // Change if your control accounts differ
    private const int DefaultAR = 1100; // Accounts Receivable
    private const int DefaultAP = 2000; // Accounts Payable

    // ✅ Dynamic (recommended)
    public Task<CashflowResponseDto> GetForecastAsync(DateTime fromDate, DateTime toDate)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
            throw new InvalidOperationException("CompanyId not resolved for current user (CurrentCompany.CompanyId is 0).");
        return GetForecastAsync(companyId, fromDate, toDate);
    }

    // ✅ Old signature supported
    public async Task<CashflowResponseDto> GetForecastAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        fromDate = fromDate.Date;
        toDate = toDate.Date;
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);

        // 1) Cash/Bank accounts
        var cashAccountNos = await _db.ChartOfAccounts.AsNoTracking()
            .Where(a => a.CompanyId == companyId &&
                        (a.IsCashAccount || a.AccountType == "Cash" || a.AccountType == "Bank"))
            .Select(a => a.AccountNo)
            .ToListAsync();

        // 2) Opening cash from GL
        var openingCash = await GetOpeningCashFromGLAsync(companyId, cashAccountNos, fromDate);

        // 3) Actual IN/OUT
        var actualInByDay = await GetActualInByDayAsync(companyId, fromDate, toDate);
        var actualOutByDay = await GetActualOutByDayAsync(companyId, fromDate, toDate);

        // 4) Expected IN/OUT
        var expectedInByDay = await GetExpectedInByDayAsync(companyId, fromDate, toDate);
        var expectedOutByDay = await GetExpectedOutByDayAsync(companyId, fromDate, toDate);

        // 5) Rows
        var rows = new List<CashflowRowDto>();

        decimal running = openingCash;
        decimal worst = running;
        DateTime worstDate = fromDate;

        decimal totalActualIn = 0, totalExpectedIn = 0, totalActualOut = 0, totalExpectedOut = 0;

        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            actualInByDay.TryGetValue(d, out var aIn);
            actualOutByDay.TryGetValue(d, out var aOut);

            expectedInByDay.TryGetValue(d, out var eIn);
            expectedOutByDay.TryGetValue(d, out var eOut);

            totalActualIn += aIn;
            totalActualOut += aOut;
            totalExpectedIn += eIn;
            totalExpectedOut += eOut;

            running = running + aIn + eIn - aOut - eOut;

            if (running < worst)
            {
                worst = running;
                worstDate = d;
            }

            rows.Add(new CashflowRowDto
            {
                Date = d,
                ActualIn = aIn,
                ExpectedIn = eIn,
                ActualOut = aOut,
                ExpectedOut = eOut,
                Running = running
            });
        }

        var alerts = BuildAlerts(rows);

        return new CashflowResponseDto
        {
            CompanyId = companyId,
            FromDate = fromDate,
            ToDate = toDate,
            OpeningCash = openingCash,

            TotalActualIn = totalActualIn,
            TotalExpectedIn = totalExpectedIn,
            TotalActualOut = totalActualOut,
            TotalExpectedOut = totalExpectedOut,

            WorstBalance = worst,
            WorstBalanceDate = worstDate,

            Rows = rows,
            Alerts = alerts
        };
    }

    // ---------------------------
    // ✅ Risk Radar + Expense Donut
    // ---------------------------
    public Task<RiskRadarDto> GetRiskRadarAsync(int companyId, DateTime toDate)
    {
        var from = toDate.Date.AddMonths(-12); // 1 year lookback
        var to = toDate.Date;
        return GetRiskRadarAsync(companyId, from, to);
    }

    public async Task<RiskRadarDto> GetRiskRadarAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        if (companyId <= 0) throw new Exception("Invalid company.");

        fromDate = fromDate.Date;
        toDate = toDate.Date;
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);

        var arLines = await BuildOutstandingAsync(companyId, fromDate, toDate, DefaultAR, isReceivable: true);
        var apLines = await BuildOutstandingAsync(companyId, fromDate, toDate, DefaultAP, isReceivable: false);

        // Expense breakdown (Top 7) from GL debit side on expense accounts
        var exp = await (from g in _db.GeneralLedgerEntries.AsNoTracking()
                         join coa in _db.ChartOfAccounts.AsNoTracking()
                            on new { g.CompanyId, AccountNo = g.DebitAccountNo }
                            equals new { coa.CompanyId, coa.AccountNo }
                         where g.CompanyId == companyId
                               && g.TxnDate >= fromDate && g.TxnDate <= toDate
                               && g.Amount > 0
                               && coa.IsActive
                               && (coa.AccountType == "Expense" || (coa.AccountNo >= 5000 && coa.AccountNo <= 9999))
                         group new { g, coa } by new { coa.AccountNo, coa.AccountName } into grp
                         orderby grp.Sum(x => x.g.Amount) descending
                         select new RiskLineDto
                         {
                             Title = $"{grp.Key.AccountNo} - {grp.Key.AccountName}",
                             VoucherType = "EXP",
                             VoucherNo = grp.Key.AccountNo.ToString(),
                             RefId = null,
                             FirstTxnDate = toDate,
                             AgeDays = 0,
                             Amount = grp.Sum(x => x.g.Amount)
                         })
                        .Take(7)
                        .ToListAsync();

        return new RiskRadarDto
        {
            TopReceivables = arLines.Take(5).ToList(),
            TopPayables = apLines.Take(5).ToList(),
            TopExpenseCategories = exp
        };
    }

    private async Task<List<RiskLineDto>> BuildOutstandingAsync(
        int companyId,
        DateTime fromDate,
        DateTime toDate,
        int controlAccountNo,
        bool isReceivable)
    {
        var q = _db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                        && x.TxnDate >= fromDate && x.TxnDate <= toDate
                        && (x.DebitAccountNo == controlAccountNo || x.CreditAccountNo == controlAccountNo));

        var grouped = await q
            .GroupBy(x => new { x.RefId, x.VoucherType, x.VoucherNo })
            .Select(g => new
            {
                g.Key.RefId,
                g.Key.VoucherType,
                g.Key.VoucherNo,
                FirstDate = g.Min(x => x.TxnDate),
                Debit = g.Where(x => x.DebitAccountNo == controlAccountNo).Sum(x => x.Amount),
                Credit = g.Where(x => x.CreditAccountNo == controlAccountNo).Sum(x => x.Amount)
            })
            .ToListAsync();

        var result = grouped
            .Select(x =>
            {
                var outstanding = isReceivable ? (x.Debit - x.Credit) : (x.Credit - x.Debit);
                return new RiskLineDto
                {
                    Title = $"{x.VoucherType}-{x.VoucherNo}",
                    VoucherType = x.VoucherType ?? "",
                    VoucherNo = x.VoucherNo ?? "",
                    RefId = x.RefId,
                    FirstTxnDate = x.FirstDate,
                    AgeDays = (toDate.Date - x.FirstDate.Date).Days,
                    Amount = outstanding
                };
            })
            .Where(x => x.Amount > 0.009m)
            .OrderByDescending(x => x.Amount)
            .ToList();

        return result;
    }

    // ---------------------------
    // Helpers (Cash forecast)
    // ---------------------------
    private async Task<decimal> GetOpeningCashFromGLAsync(int companyId, List<int> cashAccountNos, DateTime fromDate)
    {
        if (cashAccountNos.Count == 0) return 0m;

        var debit = await _db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.TxnDate < fromDate && cashAccountNos.Contains(x.DebitAccountNo))
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var credit = await _db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.TxnDate < fromDate && cashAccountNos.Contains(x.CreditAccountNo))
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        return debit - credit;
    }

    private async Task<Dictionary<DateTime, decimal>> GetActualInByDayAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        var q = from r in _db.InvoiceReceipts.AsNoTracking()
                join i in _db.Invoices.AsNoTracking() on r.InvoiceId equals i.InvoiceId
                where i.CompanyId == companyId
                      && r.ReceiptDate >= fromDate && r.ReceiptDate <= toDate
                group r by r.ReceiptDate.Date into g
                select new { Day = g.Key, Amount = g.Sum(x => x.Amount) };

        return await q.ToDictionaryAsync(x => x.Day, x => x.Amount);
    }

    private async Task<Dictionary<DateTime, decimal>> GetActualOutByDayAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        var q = from p in _db.PurchasePayments.AsNoTracking()
                join pi in _db.PurchaseInvoices.AsNoTracking() on p.PurchaseInvoiceId equals pi.PurchaseInvoiceId
                where pi.CompanyId == companyId
                      && p.PaymentDate >= fromDate && p.PaymentDate <= toDate
                group p by p.PaymentDate.Date into g
                select new { Day = g.Key, Amount = g.Sum(x => x.Amount) };

        return await q.ToDictionaryAsync(x => x.Day, x => x.Amount);
    }

    private async Task<Dictionary<DateTime, decimal>> GetExpectedInByDayAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        var dict = new Dictionary<DateTime, decimal>();

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId)
            .Select(i => new { i.InvoiceId, i.InvoiceDate, i.DueDate, i.GrandTotal })
            .ToListAsync();

        if (invoices.Count == 0) return dict;

        var ids = invoices.Select(x => x.InvoiceId).ToList();

        var paidMap = await _db.InvoiceReceipts.AsNoTracking()
            .Where(r => ids.Contains(r.InvoiceId))
            .GroupBy(r => r.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Paid = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Paid);

        foreach (var inv in invoices)
        {
            var due = (inv.DueDate ?? inv.InvoiceDate.AddDays(30)).Date;
            if (due < fromDate || due > toDate) continue;

            paidMap.TryGetValue(inv.InvoiceId, out var paid);
            var outstanding = inv.GrandTotal - paid;
            if (outstanding <= 0) continue;

            dict.TryGetValue(due, out var cur);
            dict[due] = cur + outstanding;
        }

        return dict;
    }

    private async Task<Dictionary<DateTime, decimal>> GetExpectedOutByDayAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        var dict = new Dictionary<DateTime, decimal>();

        var purchases = await _db.PurchaseInvoices.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .Select(p => new { p.PurchaseInvoiceId, p.PurchaseDate, p.DueDate, p.GrandTotal })
            .ToListAsync();

        if (purchases.Count == 0) return dict;

        var ids = purchases.Select(x => x.PurchaseInvoiceId).ToList();

        var paidMap = await _db.PurchasePayments.AsNoTracking()
            .Where(x => ids.Contains(x.PurchaseInvoiceId))
            .GroupBy(x => x.PurchaseInvoiceId)
            .Select(g => new { PurchaseInvoiceId = g.Key, Paid = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.PurchaseInvoiceId, x => x.Paid);

        foreach (var p in purchases)
        {
            var due = (p.DueDate ?? p.PurchaseDate.AddDays(30)).Date;
            if (due < fromDate || due > toDate) continue;

            paidMap.TryGetValue(p.PurchaseInvoiceId, out var paid);
            var outstanding = p.GrandTotal - paid;
            if (outstanding <= 0) continue;

            dict.TryGetValue(due, out var cur);
            dict[due] = cur + outstanding;
        }

        return dict;
    }

    private static List<CashflowAlertDto> BuildAlerts(List<CashflowRowDto> rows)
    {
        var list = new List<CashflowAlertDto>();

        foreach (var r in rows)
        {
            if (r.Running < 0)
            {
                list.Add(new CashflowAlertDto
                {
                    Date = r.Date,
                    Severity = "danger",
                    Message = $"Cash deficit: {Math.Abs(r.Running):0.00}"
                });
                continue;
            }

            if (r.ExpectedOut > 0 && r.Running < (r.ExpectedOut * 0.25m))
            {
                list.Add(new CashflowAlertDto
                {
                    Date = r.Date,
                    Severity = "warning",
                    Message = $"High expected out: {r.ExpectedOut:0.00}"
                });
            }
        }

        return list.OrderBy(x => x.Date).Take(12).ToList();
    }
}
