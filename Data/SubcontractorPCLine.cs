using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class SubcontractorPCLine
    {
        public int SubcontractorPCLineId { get; set; }
        public int CompanyId { get; set; }

        public int SubcontractorPCId { get; set; }
        public SubcontractorPC? PC { get; set; }

        [Required, MaxLength(200)]
        public string WorkSection { get; set; } = "";  // e.g., Slab/Wall/Columns

        public decimal PreviousAmount { get; set; } = 0m;
        public decimal CurrentAmount { get; set; } = 0m;

        // stored for convenience (Previous + Current)
        public decimal TotalAmount { get; set; } = 0m;

        [MaxLength(200)]
        public string? Remarks { get; set; }
    }
}
