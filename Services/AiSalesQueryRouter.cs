using System.Text.RegularExpressions;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Services;

public sealed class AiSalesQueryRouter
{
    private readonly ICurrentCompany _currentCompany;

    public AiSalesQueryRouter(ICurrentCompany currentCompany)
    {
        _currentCompany = currentCompany;
    }

    // ✅ companyId param removed — always uses CurrentCompany.CompanyId
    public Task<(bool Supported, string? Clarify, AiSalesQuerySpec? Spec)> RouteAsync(string text)
    {
        // ✅ Ensure company selected (dynamic)
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
            return Task.FromResult<(bool, string?, AiSalesQuerySpec?)>((false, "Company not selected.", null));

        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult<(bool, string?, AiSalesQuerySpec?)>((false, null, null));

        var q = Normalize(text);

        // Sales intent detect
        var isSales = q.Contains("sales") || q.Contains("sale") || q.Contains("revenue") || q.Contains("turnover");
        if (!isSales)
            return Task.FromResult<(bool, string?, AiSalesQuerySpec?)>((false, null, null));

        var spec = new AiSalesQuerySpec
        {
            IsSalesQuery = true,
            Explanation = "Parsed by AiSalesQueryRouter (dynamic company via CurrentCompany)"
        };

        // ---- top N ----
        spec.Top = ParseTopN(q);

        // ---- group by ----
        spec.GroupBy = ParseGroupBy(q) ?? "none";

        // ---- range ----
        var lastDays = ParseLastNDays(q);
        if (lastDays.HasValue)
        {
            spec.LastNDays = lastDays.Value;
        }
        else
        {
            var (from, to, ok) = ParseRange(q);
            if (ok)
            {
                spec.FromDate = from;
                spec.ToDate = to;
            }
            else
            {
                // default: this month
                var today = DateTime.Today;
                spec.FromDate = new DateTime(today.Year, today.Month, 1);
                spec.ToDate = today;
            }
        }

        // ---- sorting defaults ----
        if (spec.GroupBy == "day")
        {
            spec.SortBy = "date";
            spec.SortDir = "asc";
        }
        else
        {
            spec.SortBy = "sales_total";
            spec.SortDir = "desc";
        }

        return Task.FromResult<(bool, string?, AiSalesQuerySpec?)>((true, null, spec));
    }

    private static string Normalize(string s)
        => Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");

    private static int? ParseTopN(string q)
    {
        var m = Regex.Match(q, @"top\s+(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > 0)
            return n;
        return null;
    }

    private static int? ParseLastNDays(string q)
    {
        var m = Regex.Match(q, @"last\s+(\d+)\s+day");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > 0)
            return n;

        m = Regex.Match(q, @"last\s+(\d+)\s+week");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var w) && w > 0)
            return w * 7;

        return null;
    }

    private static string? ParseGroupBy(string q)
    {
        if (q.Contains("by customer") || q.Contains("by client"))
            return "customer";

        if (q.Contains("by day") || q.Contains("daily"))
            return "day";

        if (q.Contains("total") || q.Contains("overall"))
            return "none";

        return null;
    }

    private static (DateTime from, DateTime to, bool ok) ParseRange(string q)
    {
        var today = DateTime.Today;

        if (q.Contains("today"))
            return (today, today, true);

        if (q.Contains("yesterday"))
        {
            var y = today.AddDays(-1);
            return (y, y, true);
        }

        if (q.Contains("this month"))
        {
            var from = new DateTime(today.Year, today.Month, 1);
            return (from, today, true);
        }

        if (q.Contains("last month"))
        {
            var firstThis = new DateTime(today.Year, today.Month, 1);
            var lastMonthEnd = firstThis.AddDays(-1);
            var lastMonthStart = new DateTime(lastMonthEnd.Year, lastMonthEnd.Month, 1);
            return (lastMonthStart, lastMonthEnd, true);
        }

        return (default, default, false);
    }
}
