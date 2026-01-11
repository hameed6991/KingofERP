using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class ConstructionProject
    {
        public int ConstructionProjectId { get; set; }

        // ✅ MUST: Company scope
        public int CompanyId { get; set; }

        [Required, MaxLength(30)]
        public string ProjectCode { get; set; } = "";

        [Required, MaxLength(150)]
        public string ProjectName { get; set; } = "";

        [MaxLength(150)]
        public string? ClientName { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; }

        public decimal VatRate { get; set; } = 0.05m; // UAE default

        [MaxLength(15)]
        public string Status { get; set; } = "Active";  // Active / OnHold / Closed

        public bool IsArchived { get; set; } = false;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}
