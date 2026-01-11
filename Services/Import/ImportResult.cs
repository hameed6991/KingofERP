namespace UaeEInvoice.Services.Import;

public class ImportResult
{
    public int TotalRows { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = new();
}

public static class ImportHelpers
{
    public static string Clean(this string? s) => (s ?? "").Trim();

    public static bool ToBool(this string? s)
    {
        var v = (s ?? "").Trim().ToLowerInvariant();
        return v is "1" or "true" or "yes" or "y";
    }

    public static decimal ToDecimal(this string? s)
        => decimal.TryParse((s ?? "").Trim(), out var d) ? d : 0;

    public static int ToInt(this string? s)
        => int.TryParse((s ?? "").Trim(), out var n) ? n : 0;
}
