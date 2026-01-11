using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Analytics;

public class AnalyticsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public AnalyticsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    private static DateTime MonthStart(DateTime d) => new(d.Year, d.Month, 1);

    // ------------------------------
    // Helpers (NO tuples - EF safe)
    // ------------------------------
    private sealed class ReceiptAggRow
    {
        public int InvoiceId { get; set; }
        public decimal Paid { get; set; }
        public DateTime? LastReceiptDate { get; set; }
    }

    private sealed class PayAggRow
    {
        public int PurchaseInvoiceId { get; set; }
        public decimal Paid { get; set; }
    }

    // InvoiceReceipt may NOT have CompanyId -> join with Invoices to filter by companyId safely
    private static IQueryable<ReceiptAggRow> ReceiptAgg(AppDbContext db, int companyId)
    {
        return
            from r in db.InvoiceReceipts.AsNoTracking()
            join inv in db.Invoices.AsNoTracking() on r.InvoiceId equals inv.InvoiceId
            where inv.CompanyId == companyId
            group r by r.InvoiceId into g
            select new ReceiptAggRow
            {
                InvoiceId = g.Key,
                Paid = g.Sum(x => x.Amount),
                LastReceiptDate = g.Max(x => (DateTime?)x.ReceiptDate)
            };
    }

    // PurchasePayment usually relates to PurchaseInvoice -> join to filter companyId safely
    private static IQueryable<PayAggRow> PaymentAgg(AppDbContext db, int companyId)
    {
        return
            from p in db.PurchasePayments.AsNoTracking()
            join pinv in db.PurchaseInvoices.AsNoTracking() on p.PurchaseInvoiceId equals pinv.PurchaseInvoiceId
            where pinv.CompanyId == companyId
            group p by p.PurchaseInvoiceId into g
            select new PayAggRow
            {
                PurchaseInvoiceId = g.Key,
                Paid = g.Sum(x => x.Amount)
            };
    }

    // ------------------------------
    // Executive KPIs (MTD + Aging + VAT)
    // ------------------------------
    public async Task<ExecKpis> GetExecutiveKpis(int companyId, DateTime asOf)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var fromDate = MonthStart(asOf);
        var toExcl = asOf.Date.AddDays(1);

        // Sales MTD + VAT Output
        var salesMtd = await db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.InvoiceDate >= fromDate && x.InvoiceDate < toExcl)
            .SumAsync(x => (decimal?)x.GrandTotal) ?? 0m;

        var vatOut = await db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.InvoiceDate >= fromDate && x.InvoiceDate < toExcl)
            .SumAsync(x => (decimal?)x.VatTotal) ?? 0m;

        // Purchases MTD + VAT Input
        var purchasesMtd = await db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.PurchaseDate >= fromDate && x.PurchaseDate < toExcl)
            .SumAsync(x => (decimal?)x.GrandTotal) ?? 0m;

        var vatIn = await db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.PurchaseDate >= fromDate && x.PurchaseDate < toExcl)
            .SumAsync(x => (decimal?)x.VatTotal) ?? 0m;

        // AR balances (Invoice total - paid)
        var receiptAgg = ReceiptAgg(db, companyId);

        var arBalances =
            from inv in db.Invoices.AsNoTracking()
            where inv.CompanyId == companyId
            join ra in receiptAgg on inv.InvoiceId equals ra.InvoiceId into rj
            from ra in rj.DefaultIfEmpty()
            let paid = (decimal?)ra.Paid ?? 0m
            let bal = inv.GrandTotal - paid
            select new { inv.InvoiceId, inv.DueDate, Balance = bal };

        var arOutstanding = await arBalances
            .Where(x => x.Balance > 0)
            .SumAsync(x => (decimal?)x.Balance) ?? 0m;

        var arOverdue = await arBalances
            .Where(x => x.DueDate != null && x.DueDate.Value.Date < asOf.Date && x.Balance > 0)
            .SumAsync(x => (decimal?)x.Balance) ?? 0m;

        // AP balances (Purchase total - paid)
        var payAgg = PaymentAgg(db, companyId);

        var apBalances =
            from pinv in db.PurchaseInvoices.AsNoTracking()
            where pinv.CompanyId == companyId
            join pa in payAgg on pinv.PurchaseInvoiceId equals pa.PurchaseInvoiceId into pj
            from pa in pj.DefaultIfEmpty()
            let paid = (decimal?)pa.Paid ?? 0m
            let bal = pinv.GrandTotal - paid
            select new { pinv.PurchaseInvoiceId, pinv.DueDate, Balance = bal };

        var apOutstanding = await apBalances
            .Where(x => x.Balance > 0)
            .SumAsync(x => (decimal?)x.Balance) ?? 0m;

        var apOverdue = await apBalances
            .Where(x => x.DueDate != null && x.DueDate.Value.Date < asOf.Date && x.Balance > 0)
            .SumAsync(x => (decimal?)x.Balance) ?? 0m;

        var vatNet = vatOut - vatIn;

        return new ExecKpis(
            SalesMtd: salesMtd,
            PurchasesMtd: purchasesMtd,
            ArOutstanding: arOutstanding,
            ArOverdue: arOverdue,
            ApOutstanding: apOutstanding,
            ApOverdue: apOverdue,
            VatOutputMtd: vatOut,
            VatInputMtd: vatIn,
            VatNetMtd: vatNet
        );
    }

    // ------------------------------
    // Sales Trend Daily (Last N days)
    // ------------------------------
    public async Task<List<TrendPoint>> GetSalesTrendDaily(int companyId, int days, DateTime asOf)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var start = asOf.Date.AddDays(-(days - 1));
        var endExcl = asOf.Date.AddDays(1);

        var rows = await db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.InvoiceDate >= start && x.InvoiceDate < endExcl)
            .GroupBy(x => x.InvoiceDate.Date)
            .Select(g => new TrendPoint(g.Key, g.Sum(x => x.GrandTotal), g.Count()))
            .ToListAsync();

        var map = rows.ToDictionary(x => x.Day.Date);

        var list = new List<TrendPoint>();
        for (var d = start; d < endExcl; d = d.AddDays(1))
            list.Add(map.TryGetValue(d.Date, out var tp) ? tp : new TrendPoint(d.Date, 0m, 0));

        return list;
    }

    // ------------------------------
    // AR Aging
    // ------------------------------
    public async Task<AgingResult> GetArAging(int companyId, DateTime asOf)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var today = asOf.Date;

        var receiptAgg = ReceiptAgg(db, companyId);

        var open = await (
            from inv in db.Invoices.AsNoTracking()
            where inv.CompanyId == companyId
            join ra in receiptAgg on inv.InvoiceId equals ra.InvoiceId into rj
            from ra in rj.DefaultIfEmpty()
            let paid = (decimal?)ra.Paid ?? 0m
            let bal = inv.GrandTotal - paid
            where bal > 0
            select new { inv.InvoiceId, inv.DueDate, Balance = bal }
        ).ToListAsync();

        int DaysOverdue(DateTime? due) => due == null ? 0 : (today - due.Value.Date).Days;

        decimal SumBucket(Func<int, bool> pred) => open.Where(x => pred(DaysOverdue(x.DueDate))).Sum(x => x.Balance);
        int CountBucket(Func<int, bool> pred) => open.Count(x => pred(DaysOverdue(x.DueDate)));

        var buckets = new List<AgingBucket>
        {
            new("0–30",  SumBucket(d => d >= 0  && d <= 30),  CountBucket(d => d >= 0  && d <= 30)),
            new("31–60", SumBucket(d => d >= 31 && d <= 60),  CountBucket(d => d >= 31 && d <= 60)),
            new("61–90", SumBucket(d => d >= 61 && d <= 90),  CountBucket(d => d >= 61 && d <= 90)),
            new("90+",   SumBucket(d => d >= 91),             CountBucket(d => d >= 91))
        };

        return new AgingResult(buckets, buckets.Sum(x => x.Amount));
    }

    // ------------------------------
    // AP Aging
    // ------------------------------
    public async Task<AgingResult> GetApAging(int companyId, DateTime asOf)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var today = asOf.Date;

        var payAgg = PaymentAgg(db, companyId);

        var open = await (
            from pinv in db.PurchaseInvoices.AsNoTracking()
            where pinv.CompanyId == companyId
            join pa in payAgg on pinv.PurchaseInvoiceId equals pa.PurchaseInvoiceId into pj
            from pa in pj.DefaultIfEmpty()
            let paid = (decimal?)pa.Paid ?? 0m
            let bal = pinv.GrandTotal - paid
            where bal > 0
            select new { pinv.PurchaseInvoiceId, pinv.DueDate, Balance = bal }
        ).ToListAsync();

        int DaysOverdue(DateTime? due) => due == null ? 0 : (today - due.Value.Date).Days;

        decimal SumBucket(Func<int, bool> pred) => open.Where(x => pred(DaysOverdue(x.DueDate))).Sum(x => x.Balance);
        int CountBucket(Func<int, bool> pred) => open.Count(x => pred(DaysOverdue(x.DueDate)));

        var buckets = new List<AgingBucket>
        {
            new("0–30",  SumBucket(d => d >= 0  && d <= 30),  CountBucket(d => d >= 0  && d <= 30)),
            new("31–60", SumBucket(d => d >= 31 && d <= 60),  CountBucket(d => d >= 31 && d <= 60)),
            new("61–90", SumBucket(d => d >= 61 && d <= 90),  CountBucket(d => d >= 61 && d <= 90)),
            new("90+",   SumBucket(d => d >= 91),             CountBucket(d => d >= 91))
        };

        return new AgingResult(buckets, buckets.Sum(x => x.Amount));
    }

    // ------------------------------
    // Top Customers (pass from/to yourself)
    // ------------------------------
    public async Task<List<TopCustomerRow>> GetTopCustomers(int companyId, DateTime fromDate, DateTime toDate, int take = 10)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // NOTE: Keep your TopCustomerRow constructor exactly as your model expects.
        // If your record is (CustomerId, CustomerName, SalesTotal, InvoiceCount) this matches FinanceHealth page you shared earlier.
        var rows = await (
            from inv in db.Invoices.AsNoTracking()
            join c in db.Customers.AsNoTracking() on inv.CustomerId equals c.CustomerId
            where inv.CompanyId == companyId
               && inv.InvoiceDate >= fromDate
               && inv.InvoiceDate < toDate
            group inv by new { inv.CustomerId, c.Name } into g
            orderby g.Sum(x => x.GrandTotal) descending
            select new TopCustomerRow(
                g.Key.CustomerId,
                g.Key.Name,
                g.Sum(x => x.GrandTotal),
                g.Count()
            )
        ).Take(take).ToListAsync();

        return rows;
    }

    // ------------------------------
    // Customer Sales (MTD) - used by CustomerSales.razor
    // ------------------------------
    public async Task<CustomerSalesMtdResult> GetCustomerSalesMtd(int companyId, int customerId, DateTime asOf)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var fromDate = MonthStart(asOf);
        var toExcl = asOf.Date.AddDays(1);

        var cust = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.CustomerId == customerId);

        if (cust == null)
            throw new Exception("Customer not found.");

        var receiptAgg = ReceiptAgg(db, companyId);

        var invoices = await (
            from inv in db.Invoices.AsNoTracking()
            where inv.CompanyId == companyId
               && inv.CustomerId == customerId
               && inv.InvoiceDate >= fromDate
               && inv.InvoiceDate < toExcl
            join ra in receiptAgg on inv.InvoiceId equals ra.InvoiceId into rj
            from ra in rj.DefaultIfEmpty()
            let paid = (decimal?)ra.Paid ?? 0m
            let bal = inv.GrandTotal - paid
            let status =
                bal <= 0 ? "Paid" :
                (inv.DueDate != null && inv.DueDate.Value.Date < asOf.Date ? "Overdue" : "Open")
            orderby inv.InvoiceDate descending, inv.InvoiceId descending
            select new CustomerInvoiceRow(
                inv.InvoiceId,
                inv.InvoiceNo,
                inv.InvoiceDate,
                inv.DueDate,
                inv.GrandTotal,
                paid,
                bal,
                status
            )
        ).ToListAsync();

        var salesMtd = invoices.Sum(x => x.Total);
        var paidToDate = invoices.Sum(x => x.Paid);
        var balance = invoices.Where(x => x.Balance > 0).Sum(x => x.Balance);

        return new CustomerSalesMtdResult(
            Customer: cust,
            From: fromDate,
            ToExclusive: toExcl,
            SalesMtd: salesMtd,
            PaidToDateOnMtdInvoices: paidToDate,
            BalanceOnMtdInvoices: balance,
            InvoiceCount: invoices.Count,
            Invoices: invoices
        );
    }

    // =========================================================
    // ✅ AR Collection (Recovery BI) - NEW, EF-safe, no translation error
    // =========================================================
    public async Task<ArRecoveryDashboardDto> GetArRecoveryDashboard(int companyId, DateTime asOfDate, int windowDays = 30, int takeTop = 10)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        if (windowDays < 7) windowDays = 7;
        if (windowDays > 180) windowDays = 180;
        if (takeTop < 5) takeTop = 5;
        if (takeTop > 50) takeTop = 50;

        var asOf = asOfDate.Date;
        var windowFrom = asOf.AddDays(-(windowDays - 1));
        var windowToExcl = asOf.AddDays(1);

        // Sales in window (for DSO)
        var salesInWindow = await db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.InvoiceDate >= windowFrom && x.InvoiceDate < windowToExcl)
            .SumAsync(x => (decimal?)x.GrandTotal) ?? 0m;

        var receiptAgg = ReceiptAgg(db, companyId);

        // Pull open invoice rows to memory (prevents EF translate error)
        var openRows = await (
            from inv in db.Invoices.AsNoTracking()
            join c in db.Customers.AsNoTracking() on inv.CustomerId equals c.CustomerId
            join ra in receiptAgg on inv.InvoiceId equals ra.InvoiceId into rj
            from ra in rj.DefaultIfEmpty()
            where inv.CompanyId == companyId && c.CompanyId == companyId
            let paid = (decimal?)ra.Paid ?? 0m
            let bal = inv.GrandTotal - paid
            where bal > 0
            select new
            {
                inv.InvoiceId,
                inv.CustomerId,
                CustomerName = c.Name,
                c.Mobile,
                inv.InvoiceDate,
                inv.DueDate,
                Balance = bal
            }
        ).ToListAsync();

        int DaysOverdue(DateTime invoiceDate, DateTime? dueDate)
        {
            var baseDate = (dueDate ?? invoiceDate).Date;
            return (asOf - baseDate).Days;
        }

        var arOutstanding = openRows.Sum(x => x.Balance);

        var overdueRows = openRows
            .Select(x => new
            {
                x.CustomerId,
                x.CustomerName,
                x.Mobile,
                x.InvoiceId,
                x.InvoiceDate,
                x.DueDate,
                x.Balance,
                OverdueDays = DaysOverdue(x.InvoiceDate, x.DueDate)
            })
            .Where(x => x.OverdueDays > 0)
            .ToList();

        var arOverdue = overdueRows.Sum(x => x.Balance);

        var topOverdue = overdueRows
            .GroupBy(x => new { x.CustomerId, x.CustomerName, x.Mobile })
            .Select(g => new OverdueCustomerRow(
                g.Key.CustomerId,
                g.Key.CustomerName,
                g.Key.Mobile,
                g.Sum(z => z.Balance),
                g.Select(z => z.InvoiceId).Distinct().Count(),
                g.Max(z => z.OverdueDays)
            ))
            .OrderByDescending(x => x.OverdueAmount)
            .Take(takeTop)
            .ToList();

        // Promise Tracker from Customer master
        var followUps = await db.Customers.AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.NextFollowUpDate != null)
            .OrderBy(c => c.NextFollowUpDate)
            .Select(c => new FollowUpRow(
                c.CustomerId,
                c.Name,
                c.Mobile,
                c.NextFollowUpDate,
                c.NextFollowUpNote
            ))
            .Take(50)
            .ToListAsync();

        // DSO
        var dsoDays = (salesInWindow <= 0m) ? 0d : (double)((arOutstanding / salesInWindow) * windowDays);

        // Trend (this month vs last month) - compute in C# safely
        DateTime dueOrInv(DateTime invDate, DateTime? due) => (due ?? invDate).Date;

        var thisMonthStart = new DateTime(asOf.Year, asOf.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var daysCount = (asOf - thisMonthStart).Days + 1;
        if (daysCount < 1) daysCount = 1;

        var overdueByDay = overdueRows
            .GroupBy(x => dueOrInv(x.InvoiceDate, x.DueDate))
            .ToDictionary(g => g.Key, g => g.Sum(z => z.Balance));

        var thisDays = Enumerable.Range(0, daysCount).Select(i => thisMonthStart.AddDays(i)).ToList();
        var lastDays = Enumerable.Range(0, daysCount).Select(i => lastMonthStart.AddDays(i)).ToList();

        var trendThis = thisDays
            .Select(d => new TrendPoint(d, overdueByDay.TryGetValue(d, out var v) ? v : 0m, 0))
            .ToList();

        var trendLast = lastDays
            .Select(d => new TrendPoint(d, overdueByDay.TryGetValue(d, out var v) ? v : 0m, 0))
            .ToList();

        return new ArRecoveryDashboardDto(
            AsOf: asOf,
            WindowDays: windowDays,
            SalesInWindow: salesInWindow,
            ArOutstanding: arOutstanding,
            ArOverdue: arOverdue,
            DsoDays: dsoDays,
            TopOverdueCustomers: topOverdue,
            FollowUps: followUps,
            OverdueTrendThisMonth: trendThis,
            OverdueTrendLastMonth: trendLast
        );
    }
}
