using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Services;

public sealed class AiSalesQueryRunner
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;

    public AiSalesQueryRunner(AppDbContext db, ICurrentCompany currentCompany)
    {
        _db = db;
        _currentCompany = currentCompany;
    }

    private sealed class InvoiceFact
    {
        public DateTime TxnDate { get; set; }
        public string Customer { get; set; } = "";
        public decimal Total { get; set; }
        public decimal Vat { get; set; }
    }

    // ✅ companyId param removed — always uses CurrentCompany.CompanyId
    public async Task<AiReportResult> RunAsync(AiSalesQuerySpec spec)
    {
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
        {
            return new AiReportResult
            {
                ReportKey = "sales_query",
                Title = "Sales",
                Columns = new() { "Note" },
                Rows = new() { new Dictionary<string, object?> { ["Note"] = "Company not selected." } },
                Note = "CurrentCompany.CompanyId is 0. Select company first."
            };
        }

        // 1) Resolve date range
        var (from, to) = ResolveRange(spec);

        // 2) Ensure Invoice entity exists
        if (_db.Model.FindEntityType(typeof(Invoice)) is null)
        {
            return new AiReportResult
            {
                ReportKey = "sales_query",
                Title = "Sales",
                Columns = new() { "Note" },
                Rows = new() { new Dictionary<string, object?> { ["Note"] = "Invoice entity not found." } },
                Note = "Your EF model doesn't contain Invoice."
            };
        }

        // 3) Detect properties (adapt to your schema)
        var dateProp = PickProperty(typeof(Invoice), "InvoiceDate", "TxnDate", "Date");
        var custProp = PickProperty(typeof(Invoice), "CustomerName", "Customer", "PartyName", "AccountName");
        var totalProp = PickProperty(typeof(Invoice), "TotalAmount", "GrandTotal", "NetAmount", "Total");
        var vatProp = PickProperty(typeof(Invoice), "VatAmount", "VATAmount", "Vat", "TaxAmount", "Tax");

        if (dateProp is null || totalProp is null)
        {
            return new AiReportResult
            {
                ReportKey = "sales_query",
                Title = "Sales",
                Columns = new() { "Note" },
                Rows = new() { new Dictionary<string, object?> { ["Note"] = "Invoice date/total fields not detected." } },
                Note = "Expected date: InvoiceDate/TxnDate and total: TotalAmount/GrandTotal."
            };
        }

        // 4) Load minimal invoice facts (✅ company filtered dynamically)
        var facts = await _db.Set<Invoice>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Where(x => EF.Property<DateTime>(x, dateProp) >= from && EF.Property<DateTime>(x, dateProp) <= to)
            .Select(x => new InvoiceFact
            {
                TxnDate = EF.Property<DateTime>(x, dateProp),
                Customer = custProp == null ? "" : (EF.Property<string>(x, custProp) ?? ""),
                Total = (decimal?)(EF.Property<decimal>(x, totalProp)) ?? 0m,
                Vat = vatProp == null ? 0m : ((decimal?)(EF.Property<decimal>(x, vatProp)) ?? 0m)
            })
            .ToListAsync();

        // 5) Decide grouping
        var groupBy = (spec.GroupBy ?? "none").ToLowerInvariant();
        if (groupBy == "customer" && custProp is null)
            groupBy = "none";

        // 6) Aggregate
        IEnumerable<dynamic> grouped;

        if (groupBy == "customer")
        {
            grouped = facts
                .Where(f => !string.IsNullOrWhiteSpace(f.Customer))
                .GroupBy(f => f.Customer.Trim())
                .Select(g => new
                {
                    Key = g.Key,
                    SalesTotal = g.Sum(x => x.Total),
                    SalesVAT = g.Sum(x => x.Vat),
                    InvoiceCount = g.Count()
                });
        }
        else if (groupBy == "day")
        {
            grouped = facts
                .GroupBy(f => f.TxnDate.Date)
                .Select(g => new
                {
                    Key = g.Key.ToString("yyyy-MM-dd"),
                    SalesTotal = g.Sum(x => x.Total),
                    SalesVAT = g.Sum(x => x.Vat),
                    InvoiceCount = g.Count()
                });
        }
        else
        {
            grouped = new[]
            {
                new
                {
                    Key = "TOTAL",
                    SalesTotal = facts.Sum(x => x.Total),
                    SalesVAT = facts.Sum(x => x.Vat),
                    InvoiceCount = facts.Count
                }
            };
        }

        // 7) Sorting
        grouped = ApplySort(grouped, spec.SortBy, spec.SortDir);

        // 8) Top N
        var top = spec.Top ?? 0;
        if (top > 0 && groupBy == "customer")
            grouped = grouped.Take(Math.Clamp(top, 1, 50));

        // 9) Shape result
        var columns = groupBy switch
        {
            "customer" => new List<string> { "Customer", "SalesTotal", "SalesVAT", "InvoiceCount" },
            "day" => new List<string> { "Date", "SalesTotal", "SalesVAT", "InvoiceCount" },
            _ => new List<string> { "Label", "SalesTotal", "SalesVAT", "InvoiceCount" }
        };

        var rows = grouped.Select(x => new Dictionary<string, object?>
        {
            [columns[0]] = x.Key,
            ["SalesTotal"] = x.SalesTotal,
            ["SalesVAT"] = x.SalesVAT,
            ["InvoiceCount"] = x.InvoiceCount
        }).ToList();

        return new AiReportResult
        {
            ReportKey = "sales_query",
            Title = $"Sales ({from:yyyy-MM-dd} → {to:yyyy-MM-dd})",
            Columns = columns,
            Rows = rows,
            Note = spec.Explanation ?? "Generated from Sales semantic query."
        };
    }

    private (DateTime from, DateTime to) ResolveRange(AiSalesQuerySpec spec)
    {
        var today = DateTime.Today;

        // last N days (inclusive)
        if (spec.LastNDays is int n && n > 0)
        {
            var from = today.AddDays(-(n - 1));
            return (from.Date, today.Date);
        }

        // explicit from/to dates
        if (spec.FromDate.HasValue && spec.ToDate.HasValue)
        {
            var f = spec.FromDate.Value.Date;
            var t = spec.ToDate.Value.Date;
            if (t < f) (f, t) = (t, f);
            return (f, t);
        }

        // fallback
        return (today.AddDays(-13).Date, today.Date);
    }

    private IEnumerable<dynamic> ApplySort(IEnumerable<dynamic> grouped, string? sortBy, string? sortDir)
    {
        var sb = (sortBy ?? "sales_total").ToLowerInvariant();
        var sd = (sortDir ?? "desc").ToLowerInvariant();

        Func<dynamic, object> keySelector = sb switch
        {
            "vat_total" => x => x.SalesVAT,
            "invoice_count" => x => x.InvoiceCount,
            "date" => x => x.Key,
            _ => x => x.SalesTotal
        };

        return sd == "asc"
            ? grouped.OrderBy(keySelector)
            : grouped.OrderByDescending(keySelector);
    }

    private string? PickProperty(Type entityClrType, params string[] names)
    {
        var et = _db.Model.FindEntityType(entityClrType);
        if (et is null) return null;

        foreach (var n in names)
            if (et.FindProperty(n) is not null)
                return n;

        return null;
    }
}
