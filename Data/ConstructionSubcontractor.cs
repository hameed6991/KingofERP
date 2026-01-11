using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class ConstructionSubcontractor
    {
        public int ConstructionSubcontractorId { get; set; }
        public int CompanyId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(30)]
        public string? TRN { get; set; }

        [Range(0, 1)]
        public decimal DefaultRetentionRate { get; set; } = 0.10m; // 10%

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(120)]
        public string? Email { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
