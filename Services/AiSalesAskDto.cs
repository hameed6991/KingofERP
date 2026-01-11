using System.Text.Json.Serialization;

namespace UaeEInvoice.Services;

public sealed class AiSalesAskDto
{
    public int CompanyId { get; set; }

    // ✅ Program.cs / UI convenience
    public string Text { get; set; } = "";

    // optional alias (if you used Question somewhere)
    [JsonIgnore]
    public string Question
    {
        get => Text;
        set => Text = value;
    }
}
