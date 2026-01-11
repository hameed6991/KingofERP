using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Services;

public sealed class AiReportRunner
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentCompany _currentCompany;

    public AiReportRunner(IDbContextFactory<AppDbContext> dbFactory, ICurrentCompany currentCompany)
    {
        _dbFactory = dbFactory;
        _currentCompany = currentCompany;
    }

    // ✅ companyId parameter removed — always uses CurrentCompany.CompanyId
    public async Task<AiReportResult> RunAsync(AiReportRequest req)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
        {
            return new AiReportResult
            {
                Title = "Company not selected",
                Note = "CompanyId not found in session. Please select company and try again."
            };
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        return req.ReportKey switch
        {
            "cash_in_out" => await CashInOut(db, companyId, req),
            "cash_balance_today" => await CashBalanceToday(db, companyId),
            "vat_payable_this_month" => await VatPayableThisMonth(db, companyId),
            "rent_paid_last_months" => await RentPaidLastMonths(db, companyId, req),
            "top_unpaid_customers" => await TopUnpaidCustomers(db, companyId, req),
            _ => new AiReportResult { Title = "Unknown report", Note = "This report is not supported yet." }
        };
    }

    private static DateTime ParseDate(Dictionary<string, string> a, string key, DateTime fallback)
        => a.TryGetValue(key, out var v) && DateTime.TryParse(v, out var d) ? d.Date : fallback.Date;

    private static int ParseInt(Dictionary<string, string> a, string key, int fallback)
        => a.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : fallback;

    // ---------------------------------------------------------
    // 1) Cash In/Out (baseline using GL rows)
    // ---------------------------------------------------------
    private static async Task<AiReportResult> CashInOut(AppDbContext db, int companyId, AiReportRequest req)
    {
        var from = ParseDate(req.Args, "fromDate", DateTime.Today.AddDays(-30));
        var to = ParseDate(req.Args, "toDate", DateTime.Today);

        var rows = await db.Set<GeneralLedgerEntry>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.TxnDate >= from && x.TxnDate <= to)
            .Select(x => new { Date = x.TxnDate.Date, x.Amount })
            .ToListAsync();

        var grouped = rows
            .GroupBy(x => x.Date)
            .OrderBy(g => g.Key)
            .Select(g => new Dictionary<string, object?>
            {
                ["Date"] = g.Key.ToString("yyyy-MM-dd"),
                ["Amount"] = g.Sum(x => x.Amount)
            })
            .ToList();

        return new AiReportResult
        {
            ReportKey = "cash_in_out",
            Title = $"Cash Movement (baseline) {from:yyyy-MM-dd} → {to:yyyy-MM-dd}",
            Columns = new() { "Date", "Amount" },
            Rows = grouped,
            Note = "Baseline view: sums GL amounts by day. Next upgrade: use AccountRoles Cash/Bank accounts for true cash in/out."
        };
    }

    // ---------------------------------------------------------
    // 2) Cash Balance Today (baseline using all GL rows)
    // ---------------------------------------------------------
    private static async Task<AiReportResult> CashBalanceToday(AppDbContext db, int companyId)
    {
        var upto = DateTime.Today;

        var gl = await db.Set<GeneralLedgerEntry>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.TxnDate <= upto)
            .Select(x => x.Amount)
            .ToListAsync();

        var bal = gl.Sum();

        return new AiReportResult
        {
            ReportKey = "cash_balance_today",
            Title = $"Cash/Bank Balance (baseline) as of {upto:yyyy-MM-dd}",
            Columns = new() { "AsOf", "Balance" },
            Rows = new()
            {
                new Dictionary<string, object?>
                {
                    ["AsOf"] = upto.ToString("yyyy-MM-dd"),
                    ["Balance"] = bal
                }
            },
            Note = "Baseline: total of GL amounts. Next upgrade: filter only Cash/Bank accounts from AccountRoles."
        };
    }

    // ---------------------------------------------------------
    // 3) VAT Payable This Month (Sales VAT - Purchase VAT)
    // ---------------------------------------------------------
    private static async Task<AiReportResult> VatPayableThisMonth(AppDbContext db, int companyId)
    {
        var now = DateTime.Today;
        var from = new DateTime(now.Year, now.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        // SALES VAT
        decimal salesVat = 0m;
        if (db.Model.FindEntityType(typeof(Invoice)) is not null)
        {
            var invVatProp = PickProperty(db, typeof(Invoice), "VatAmount", "VATAmount", "Vat", "TaxAmount", "Tax");
            var invDateProp = PickProperty(db, typeof(Invoice), "InvoiceDate", "TxnDate", "Date");

            if (invVatProp is not null && invDateProp is not null)
            {
                salesVat = await db.Set<Invoice>()
                    .AsNoTracking()
                    .Where(x => x.CompanyId == companyId)
                    .Where(x => EF.Property<DateTime>(x, invDateProp) >= from && EF.Property<DateTime>(x, invDateProp) <= to)
                    .SumAsync(x => (decimal?)EF.Property<decimal>(x, invVatProp)) ?? 0m;
            }
        }

        // PURCHASE VAT
        decimal purchaseVat = 0m;
        if (db.Model.FindEntityType(typeof(PurchaseInvoice)) is not null)
        {
            var pinvVatProp = PickProperty(db, typeof(PurchaseInvoice), "VatAmount", "VATAmount", "Vat", "TaxAmount", "Tax");
            var pinvDateProp = PickProperty(db, typeof(PurchaseInvoice), "PurchaseDate", "InvoiceDate", "TxnDate", "Date");

            if (pinvVatProp is not null && pinvDateProp is not null)
            {
                purchaseVat = await db.Set<PurchaseInvoice>()
                    .AsNoTracking()
                    .Where(x => x.CompanyId == companyId)
                    .Where(x => EF.Property<DateTime>(x, pinvDateProp) >= from && EF.Property<DateTime>(x, pinvDateProp) <= to)
                    .SumAsync(x => (decimal?)EF.Property<decimal>(x, pinvVatProp)) ?? 0m;
            }
        }

        var payable = salesVat - purchaseVat;

        return new AiReportResult
        {
            ReportKey = "vat_payable_this_month",
            Title = $"VAT Payable ({from:MMM yyyy})",
            Columns = new() { "Period", "SalesVAT", "PurchaseVAT", "Payable" },
            Rows = new()
            {
                new Dictionary<string, object?>
                {
                    ["Period"] = $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
                    ["SalesVAT"] = salesVat,
                    ["PurchaseVAT"] = purchaseVat,
                    ["Payable"] = payable
                }
            },
            Note = "If VAT fields are named differently in your models, tell me the property names — I’ll map it."
        };
    }

    // ---------------------------------------------------------
    // 4) Rent Paid last N months (from recurring rules)
    // ---------------------------------------------------------
    private static async Task<AiReportResult> RentPaidLastMonths(AppDbContext db, int companyId, AiReportRequest req)
    {
        var months = ParseInt(req.Args, "months", 3);
        months = Math.Clamp(months, 1, 24);

        var rules = await db.Set<RecurringCashRule>()
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.IsActive)
            .ToListAsync();

        var rent = rules
            .Where(r => (r.Name ?? "").Contains("rent", StringComparison.OrdinalIgnoreCase))
            .Select(r => new Dictionary<string, object?>
            {
                ["Name"] = r.Name ?? "Rent",
                ["Direction"] = r.Direction ?? "OUT",
                ["Amount"] = r.Amount,
                ["Frequency"] = r.Frequency ?? "",
                ["NextDate"] = r.NextDate.ToString("yyyy-MM-dd")
            })
            .ToList();

        return new AiReportResult
        {
            ReportKey = "rent_paid_last_months",
            Title = $"Rent (Recurring Rules) – last {months} months (schedule next step)",
            Columns = new() { "Name", "Direction", "Amount", "Frequency", "NextDate" },
            Rows = rent,
            Note = "Now shows Rent recurring rules. Next upgrade: expand these into month-by-month forecast schedule."
        };
    }

    // ---------------------------------------------------------
    // 5) Top Unpaid Customers
    // ---------------------------------------------------------
    private static async Task<AiReportResult> TopUnpaidCustomers(AppDbContext db, int companyId, AiReportRequest req)
    {
        var top = ParseInt(req.Args, "top", 10);
        top = Math.Clamp(top, 1, 50);

        if (db.Model.FindEntityType(typeof(Invoice)) is null)
        {
            return new AiReportResult
            {
                ReportKey = "top_unpaid_customers",
                Title = $"Top {top} Unpaid Customers",
                Columns = new() { "Customer", "UnpaidAmount" },
                Rows = new(),
                Note = "Invoice entity not found in EF model."
            };
        }

        var custProp = PickProperty(db, typeof(Invoice), "CustomerName", "Customer", "PartyName", "AccountName");
        var totalProp = PickProperty(db, typeof(Invoice), "TotalAmount", "GrandTotal", "NetAmount", "Total");
        var balProp = PickProperty(db, typeof(Invoice), "Balance", "BalanceAmount", "Outstanding", "DueAmount");

        if (custProp is null)
        {
            return new AiReportResult
            {
                ReportKey = "top_unpaid_customers",
                Title = $"Top {top} Unpaid Customers",
                Columns = new() { "Customer", "UnpaidAmount" },
                Rows = new(),
                Note = "Couldn't find customer name field in Invoice model. Expected: CustomerName / Customer / PartyName."
            };
        }

        // ✅ If Balance exists
        if (balProp is not null)
        {
            var q = await db.Set<Invoice>()
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .Select(x => new
                {
                    Customer = EF.Property<string>(x, custProp) ?? "",
                    Balance = (decimal?)EF.Property<decimal>(x, balProp) ?? 0m
                })
                .ToListAsync();

            var rows = q
                .Where(x => !string.IsNullOrWhiteSpace(x.Customer))
                .GroupBy(x => x.Customer)
                .Select(g => new { Customer = g.Key, Unpaid = g.Sum(x => x.Balance) })
                .Where(x => x.Unpaid > 0)
                .OrderByDescending(x => x.Unpaid)
                .Take(top)
                .Select(x => new Dictionary<string, object?>
                {
                    ["Customer"] = x.Customer,
                    ["UnpaidAmount"] = x.Unpaid
                })
                .ToList();

            return new AiReportResult
            {
                ReportKey = "top_unpaid_customers",
                Title = $"Top {top} Unpaid Customers",
                Columns = new() { "Customer", "UnpaidAmount" },
                Rows = rows,
                Note = "Based on Invoice Balance field."
            };
        }

        // ✅ Else compute Total - Receipts
        if (totalProp is null || db.Model.FindEntityType(typeof(InvoiceReceipt)) is null)
        {
            return new AiReportResult
            {
                ReportKey = "top_unpaid_customers",
                Title = $"Top {top} Unpaid Customers",
                Columns = new() { "Customer", "UnpaidAmount" },
                Rows = new(),
                Note = "Need either Invoice.Balance OR (Invoice.Total + InvoiceReceipt.Amount) to compute unpaid."
            };
        }

        var recAmountProp = PickProperty(db, typeof(InvoiceReceipt), "Amount", "PaidAmount", "ReceiptAmount");
        var recInvIdProp = PickProperty(db, typeof(InvoiceReceipt), "InvoiceId", "RefId");
        if (recAmountProp is null || recInvIdProp is null)
        {
            return new AiReportResult
            {
                ReportKey = "top_unpaid_customers",
                Title = $"Top {top} Unpaid Customers",
                Columns = new() { "Customer", "UnpaidAmount" },
                Rows = new(),
                Note = "InvoiceReceipt fields not detected (expected InvoiceId/RefId + Amount)."
            };
        }

        var invIdProp = PickProperty(db, typeof(Invoice), "InvoiceId");
        if (invIdProp is null)
        {
            return new AiReportResult
            {
                ReportKey = "top_unpaid_customers",
                Title = $"Top {top} Unpaid Customers",
                Columns = new() { "Customer", "UnpaidAmount" },
                Rows = new(),
                Note = "InvoiceId field not detected in Invoice model."
            };
        }

        var invs = await db.Set<Invoice>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new
            {
                InvoiceId = EF.Property<int>(x, invIdProp),
                Customer = EF.Property<string>(x, custProp) ?? "",
                Total = (decimal?)EF.Property<decimal>(x, totalProp) ?? 0m
            })
            .ToListAsync();

        var invIds = invs.Select(x => x.InvoiceId).Distinct().ToList();
        if (invIds.Count == 0)
        {
            return new AiReportResult
            {
                ReportKey = "top_unpaid_customers",
                Title = $"Top {top} Unpaid Customers",
                Columns = new() { "Customer", "UnpaidAmount" },
                Rows = new(),
                Note = "No invoices found for this company."
            };
        }

        var paidByInv = await db.Set<InvoiceReceipt>()
            .AsNoTracking()
            .Where(r => invIds.Contains(EF.Property<int>(r, recInvIdProp)))
            .GroupBy(r => EF.Property<int>(r, recInvIdProp))
            .Select(g => new
            {
                InvoiceId = g.Key,
                Paid = g.Sum(x => (decimal?)EF.Property<decimal>(x, recAmountProp)) ?? 0m
            })
            .ToListAsync();

        var paidDict = paidByInv.ToDictionary(x => x.InvoiceId, x => x.Paid);

        var result = invs
            .Where(x => !string.IsNullOrWhiteSpace(x.Customer))
            .Select(x =>
            {
                var paid = paidDict.TryGetValue(x.InvoiceId, out var p) ? p : 0m;
                var unpaid = x.Total - paid;
                return new { x.Customer, Unpaid = unpaid };
            })
            .Where(x => x.Unpaid > 0)
            .GroupBy(x => x.Customer)
            .Select(g => new { Customer = g.Key, Unpaid = g.Sum(x => x.Unpaid) })
            .OrderByDescending(x => x.Unpaid)
            .Take(top)
            .Select(x => new Dictionary<string, object?>
            {
                ["Customer"] = x.Customer,
                ["UnpaidAmount"] = x.Unpaid
            })
            .ToList();

        return new AiReportResult
        {
            ReportKey = "top_unpaid_customers",
            Title = $"Top {top} Unpaid Customers",
            Columns = new() { "Customer", "UnpaidAmount" },
            Rows = result,
            Note = "Computed as Invoice.Total - Receipts."
        };
    }

    // ---------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------
    private static string? PickProperty(AppDbContext db, Type entityClrType, params string[] names)
    {
        var et = db.Model.FindEntityType(entityClrType);
        if (et is null) return null;

        foreach (var n in names)
            if (et.FindProperty(n) is not null)
                return n;

        return null;
    }
}
