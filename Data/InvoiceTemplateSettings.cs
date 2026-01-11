using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace UaeEInvoice.Data
{
    // ✅ Column definition used by editor + preview
    public class InvoiceTemplateColumnDef
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public bool Visible { get; set; } = true;

        // left / right / center
        public string Align { get; set; } = "left";

        // optional
        public int Width { get; set; } = 0;

        // grouping (for sub columns)
        public string? GroupKey { get; set; }
        public string? GroupTitle { get; set; }
    }

    public class InvoiceTemplateSettings
    {
        // Basic
        public string Industry { get; set; } = "General";
        public string Style { get; set; } = "Modern";        // Modern/Classic/Minimal
        public string HeaderStyle { get; set; } = "Split";   // Split/Compact/Center

        // Colors
        public string AccentHex { get; set; } = "#3B82F6";
        public string PaperHex { get; set; } = "#FFFFFF";
        public string TextHex { get; set; } = "#0F172A";
        public string MutedHex { get; set; } = "#64748B";

        // UI
        public int Corner { get; set; } = 18;
        public int FontScale { get; set; } = 100;

        // Toggles
        public bool ShowLogo { get; set; } = true;
        public bool ShowTRN { get; set; } = true;
        public bool ShowQRCode { get; set; } = true;

        public bool ShowBankDetails { get; set; } = false;
        public bool ShowNotes { get; set; } = true;
        public bool ShowTerms { get; set; } = true;

        public bool ShowSignature { get; set; } = false;
        public string SignatureLabel { get; set; } = "Authorized Signature";

        // Watermark
        public bool WatermarkEnabled { get; set; } = false;
        public string WatermarkText { get; set; } = "PAID";
        public double WatermarkOpacity { get; set; } = 0.06; // ✅ double (avoid decimal/double errors)

        // Footer
        public string FooterNote { get; set; } = "Thank you for your business.";
        public string TermsHtml { get; set; } = "";

        // Columns
        public bool EnableColumnGroups { get; set; } = false;
        public List<InvoiceTemplateColumnDef> ItemColumns { get; set; } = new();

        // -------- Helpers --------
        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(Industry)) Industry = "General";
            if (string.IsNullOrWhiteSpace(Style)) Style = "Modern";
            if (string.IsNullOrWhiteSpace(HeaderStyle)) HeaderStyle = "Split";

            if (string.IsNullOrWhiteSpace(AccentHex)) AccentHex = "#3B82F6";
            if (string.IsNullOrWhiteSpace(PaperHex)) PaperHex = "#FFFFFF";
            if (string.IsNullOrWhiteSpace(TextHex)) TextHex = "#0F172A";
            if (string.IsNullOrWhiteSpace(MutedHex)) MutedHex = "#64748B";

            if (Corner < 10) Corner = 10;
            if (Corner > 34) Corner = 34;

            if (FontScale < 85) FontScale = 85;
            if (FontScale > 120) FontScale = 120;

            if (WatermarkOpacity <= 0) WatermarkOpacity = 0.06;
            if (WatermarkOpacity > 0.30) WatermarkOpacity = 0.30;

            if (ItemColumns == null) ItemColumns = new List<InvoiceTemplateColumnDef>();

            // ✅ If empty, create default invoice columns
            if (ItemColumns.Count == 0)
            {
                ItemColumns = new List<InvoiceTemplateColumnDef>
                {
                    new() { Key="item", Title="Item", Visible=true, Align="left" },
                    new() { Key="qty",  Title="Qty",  Visible=true, Align="right" },
                    new() { Key="rate", Title="Rate", Visible=true, Align="right" },
                    new() { Key="vat",  Title="VAT",  Visible=true, Align="right", GroupKey="tax", GroupTitle="Tax" },
                    new() { Key="total",Title="Total",Visible=true, Align="right" }
                };
            }

            // ✅ normalize nulls / align values
            foreach (var c in ItemColumns)
            {
                c.Key = (c.Key ?? "").Trim();
                c.Title = (c.Title ?? "").Trim();
                if (string.IsNullOrWhiteSpace(c.Align)) c.Align = "left";
                c.Align = c.Align.ToLowerInvariant();
                if (c.Align != "left" && c.Align != "right" && c.Align != "center") c.Align = "left";
            }

            // remove duplicates by key (keep first)
            ItemColumns = ItemColumns
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        public string ToJson()
        {
            EnsureDefaults();
            return JsonSerializer.Serialize(this, JsonOpts());
        }

        public static InvoiceTemplateSettings FromJson(string? json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    var s = new InvoiceTemplateSettings();
                    s.EnsureDefaults();
                    return s;
                }

                var s2 = JsonSerializer.Deserialize<InvoiceTemplateSettings>(json, JsonOpts()) ?? new InvoiceTemplateSettings();
                s2.EnsureDefaults();
                return s2;
            }
            catch
            {
                var s = new InvoiceTemplateSettings();
                s.EnsureDefaults();
                return s;
            }
        }

        private static JsonSerializerOptions JsonOpts() => new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }
}
