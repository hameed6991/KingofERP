using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class ConstructionPurchaseBill
    {
        public int ConstructionPurchaseBillId { get; set; }
        public int CompanyId { get; set; }

        [MaxLength(30)]
        public string BillNo { get; set; } = "";          // CPB-0001
        public int BillNoSeq { get; set; }

        public DateTime BillDate { get; set; } = DateTime.Today;

        // Vendor
        public int VendorId { get; set; }
        [MaxLength(200)]
        public string VendorName { get; set; } = "";
        [MaxLength(30)]
        public string? VendorTRN { get; set; }
        [MaxLength(60)]
        public string? VendorInvoiceNo { get; set; }      // supplier bill ref
        public DateTime? DueDate { get; set; }

        // Project (mandatory when DirectToSite)
        public int? ProjectId { get; set; }
        [MaxLength(200)]
        public string? ProjectName { get; set; }

        // Mode
        [MaxLength(20)]
        public string ReceiveMode { get; set; } = "Store"; // Store / DirectToSite

        // Totals
        public decimal SubTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal GrandTotal { get; set; }

        // Posting state
        [MaxLength(20)]
        public string Status { get; set; } = "Draft";     // Draft / Posted
        public DateTime? PostedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public List<ConstructionPurchaseBillLine> Lines { get; set; } = new();
    }
}
