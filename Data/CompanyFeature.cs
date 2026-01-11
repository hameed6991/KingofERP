using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class CompanyFeature
    {
        public int CompanyFeatureId { get; set; }

        public int CompanyId { get; set; }

        [Required, MaxLength(50)]
        public string FeatureKey { get; set; } = "";

        public bool IsEnabled { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // optional navigation (only if you already have Company entity navigation)
        // public Company? Company { get; set; }
    }
}
