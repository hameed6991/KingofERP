using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class Item
    {
        public int ItemId { get; set; }

        public int CompanyId { get; set; }

        [Required]
        public string ItemType { get; set; } = "Product"; // Product / Service

        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        [Required]
        public string Name { get; set; } = "";

        public string? SKU { get; set; }
        public string Unit { get; set; } = "Nos";

        public decimal CostPrice { get; set; } = 0m;
        public decimal SellingPrice { get; set; } = 0m;

        public VatCategory VatCategory { get; set; } = VatCategory.Standard;

        public decimal VatRate { get; set; } = 0.05m; // 5%
        public bool IsActive { get; set; } = true;
    }
}
