using System.Text.Json;
using System.Text.RegularExpressions;
using OpenAI.Chat;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Services;

public sealed class AiReportRouter
{
    private readonly ChatClient _chat;
    private readonly ICurrentCompany _currentCompany;

    public AiReportRouter(IConfiguration cfg, ICurrentCompany currentCompany)
    {
        _currentCompany = currentCompany;

        var apiKey = cfg["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("OpenAI key missing. Set OpenAI:ApiKey or OPENAI_API_KEY");

        _chat = new ChatClient(model: "gpt-5-mini", apiKey: apiKey);
    }

    // ✅ companyId parameter removed — always uses CurrentCompany.CompanyId
    public async Task<AiReportRequest> RouteAsync(string userText)
    {
        userText ??= "";
        var q = userText.Trim();

        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0)
        {
            return new AiReportRequest
            {
                ReportKey = "",
                ClarifyQuestion = "Company not selected / not found. Please select a company and try again.",
                Confidence = 0.0
            };
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return new AiReportRequest
            {
                ReportKey = "",
                ClarifyQuestion = "Ask a question (example: 'VAT payable this month').",
                Confidence = 0.0
            };
        }

        // ✅ 1) deterministic routing
        var deterministic = TryDeterministic(q);
        if (deterministic is not null)
            return deterministic;

        // ✅ 2) OpenAI fallback router
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var catalog = """
You MUST select ONE reportKey from this list only:
- cash_in_out
- cash_balance_today
- vat_payable_this_month
- rent_paid_last_months
- top_unpaid_customers

Rules:
- Do NOT write SQL.
- Output MUST be JSON only (no markdown, no explanation outside JSON).
- If user question missing required info, set clarifyQuestion.
- args must be simple strings (dates yyyy-MM-dd, integers as "10", etc).
- Use Today provided to resolve phrases like "this month", "last 30 days".
""";

        var schema = """
Return JSON with this shape:
{
  "reportKey": "cash_in_out|cash_balance_today|vat_payable_this_month|rent_paid_last_months|top_unpaid_customers",
  "args": { "fromDate":"yyyy-MM-dd", "toDate":"yyyy-MM-dd", "months":"3", "top":"10" },
  "clarifyQuestion": null or "string",
  "confidence": 0.0 to 1.0,
  "explanation": "one short line"
}
""";

        var msgs = new List<ChatMessage>
        {
            new SystemChatMessage(catalog + "\n" + schema),
            // ✅ dynamic companyId included (server-side)
            new UserChatMessage($"Today={today}. CompanyId={companyId}. User question: {q}")
        };

        var opt = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 300
        };

        var resp = await _chat.CompleteChatAsync(msgs, opt);

        // safer: model may return multiple parts
        var raw = string.Join("\n", resp.Value.Content.Select(c => c.Text ?? "")).Trim();

        var json = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AiReportRequest
            {
                ReportKey = "",
                ClarifyQuestion = "I couldn't understand that. Try: 'VAT payable this month' or 'Top 10 unpaid customers'.",
                Confidence = 0.0
            };
        }

        try
        {
            var req = JsonSerializer.Deserialize<AiReportRequest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return req ?? new AiReportRequest { ReportKey = "", ClarifyQuestion = "Sorry, try again.", Confidence = 0.0 };
        }
        catch
        {
            return new AiReportRequest
            {
                ReportKey = "",
                ClarifyQuestion = "I couldn't parse that. Try: 'cash in/out last 30 days'.",
                Confidence = 0.0
            };
        }
    }

    // ----------------------------
    // ✅ Deterministic routing
    // ----------------------------
    private static AiReportRequest? TryDeterministic(string q)
    {
        var t = q.Trim().ToLowerInvariant();

        // VAT payable this month
        if (t.Contains("vat") && (t.Contains("payable") || t.Contains("due")))
        {
            return new AiReportRequest
            {
                ReportKey = "vat_payable_this_month",
                Args = new(),
                ClarifyQuestion = null,
                Confidence = 0.95,
                Explanation = "VAT payable for current month"
            };
        }

        // Cash balance today
        if (t.Contains("cash balance") || t.Contains("bank balance") || t.Contains("balance today"))
        {
            return new AiReportRequest
            {
                ReportKey = "cash_balance_today",
                Args = new(),
                ClarifyQuestion = null,
                Confidence = 0.95,
                Explanation = "Cash/bank balance as of today"
            };
        }

        // Top unpaid customers
        if (t.Contains("unpaid") && (t.Contains("customer") || t.Contains("customers")))
        {
            var top = ExtractFirstInt(t) ?? 10;
            return new AiReportRequest
            {
                ReportKey = "top_unpaid_customers",
                Args = new Dictionary<string, string> { ["top"] = top.ToString() },
                ClarifyQuestion = null,
                Confidence = 0.95,
                Explanation = "Top unpaid customers"
            };
        }

        // Rent paid last X months
        if (t.Contains("rent"))
        {
            var months = ExtractFirstInt(t) ?? 3;
            return new AiReportRequest
            {
                ReportKey = "rent_paid_last_months",
                Args = new Dictionary<string, string> { ["months"] = months.ToString() },
                ClarifyQuestion = null,
                Confidence = 0.90,
                Explanation = "Rent recurring rules / schedule"
            };
        }

        // Cash in/out last N days
        if (t.Contains("cash in") || t.Contains("cash out") || t.Contains("in/out") || t.Contains("in out"))
        {
            var days = ExtractFirstInt(t) ?? 30;
            var to = DateTime.Today;
            var from = to.AddDays(-days);

            return new AiReportRequest
            {
                ReportKey = "cash_in_out",
                Args = new Dictionary<string, string>
                {
                    ["fromDate"] = from.ToString("yyyy-MM-dd"),
                    ["toDate"] = to.ToString("yyyy-MM-dd")
                },
                ClarifyQuestion = null,
                Confidence = 0.90,
                Explanation = $"Cash movement last {days} days"
            };
        }

        return null;
    }

    private static int? ExtractFirstInt(string text)
    {
        var m = Regex.Match(text, @"\b(\d{1,3})\b");
        if (!m.Success) return null;
        return int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    private static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var first = raw.IndexOf('{');
        var last = raw.LastIndexOf('}');
        if (first < 0 || last <= first) return null;

        return raw.Substring(first, last - first + 1).Trim();
    }
}
