using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class Vendor
    {
        public int VendorId { get; set; }
        public int CompanyId { get; set; }

        [Required, MaxLength(200)]
        public string VendorName { get; set; } = "";

        [MaxLength(30)]
        public string? TRN { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(400)]
        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
