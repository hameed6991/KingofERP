using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UaeEInvoice.Data
{
    public class InvoiceTemplate
    {
        public int InvoiceTemplateId { get; set; }

        public int CompanyId { get; set; }

        [Required, MaxLength(120)]
        public string Name { get; set; } = "New Template";

        public string? CustomHtml { get; set; }
        public string? CustomCss { get; set; }


        [MaxLength(60)]
        public string? IndustryTag { get; set; } = "General";

        [MaxLength(40)]
        public string BaseKey { get; set; } = "modern"; // modern / classic / etc

        public bool IsSystem { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Stored JSON
        public string SettingsJson { get; set; } = "";

        // Runtime object (not mapped)
        [NotMapped]
        public InvoiceTemplateSettings Settings
        {
            get => InvoiceTemplateSettings.FromJson(SettingsJson);
            set => SettingsJson = (value ?? new InvoiceTemplateSettings()).ToJson();
        }
    }
}
