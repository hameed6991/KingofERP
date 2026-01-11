namespace UaeEInvoice.Services;

public sealed class AiReportRequest
{
    public string ReportKey { get; set; } = "";
    public Dictionary<string, string> Args { get; set; } = new(); // all values as string (easy parsing)
    public string? ClarifyQuestion { get; set; } // if missing info
    public double Confidence { get; set; } = 0.0;
    public string? Explanation { get; set; } // short summary for UI
}

public sealed class AiReportResult
{
    public string ReportKey { get; set; } = "";
    public string Title { get; set; } = "";
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public List<string> Columns { get; set; } = new();
    public string? Note { get; set; }
}
