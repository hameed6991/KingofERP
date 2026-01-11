using System.Text.Json.Serialization;

namespace UaeEInvoice.Services;


public sealed class AiSalesRouteEnvelope
{
    public bool Supported { get; set; }
    public string? Clarify { get; set; }
    public AiSalesQuerySpec? Spec { get; set; }

    // ✅ Allows: var (supported, clarify, spec) = env;
    public void Deconstruct(out bool supported, out string? clarify, out AiSalesQuerySpec? spec)
    {
        supported = Supported;
        clarify = Clarify;
        spec = Spec;
    }
}

public sealed class AiSalesQuerySpec
{
    // Runner checks this
    public bool IsSalesQuery { get; set; } = true;

    // ✅ Runner தேடுற fields (ADD)
    public int? Top { get; set; }                 // Top 10 customers etc
    public int? LastNDays { get; set; }           // last 14 days / last 30 days
    public DateTime? FromDate { get; set; }       // explicit from
    public DateTime? ToDate { get; set; }         // explicit to

    // ✅ Grouping / Grain (day/customer/none)
    // Some code may use GroupBy, some may use Grain -> keep both
    public string? GroupBy { get; set; }          // "day" / "customer" / "none"
    public string? Grain { get => GroupBy; set => GroupBy = value; }

    // Sorting (Runner may use)
    public string? SortBy { get; set; }           // "sales_total" / "date"
    public string? SortDir { get; set; }          // "asc" / "desc"

    public string? Explanation { get; set; }

    // Optional convenience (doesn't break anything)
    public bool HasExplicitRange => FromDate.HasValue || ToDate.HasValue;
}
