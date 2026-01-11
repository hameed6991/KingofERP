// Models/PurchaseInvoice.cs


namespace UaeEInvoice.Data
{
    public class PurchaseInvoice
    {
        public int PurchaseInvoiceId { get; set; }
        public int CompanyId { get; set; }

        public string PurchaseNo { get; set; } = "";
        public DateTime PurchaseDate { get; set; }

        public int VendorId { get; set; }
        public string VendorName { get; set; } = "";
        public string? VendorTRN { get; set; }

        // ✅ ADD THIS
        public string? VendorInvoiceNo { get; set; }

        public decimal SubTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public DateTime? DueDate { get; set; }   // ✅ new (nullable)

        public int PurchaseNoSeq { get; set; }

        public List<PurchaseInvoiceLine> Lines { get; set; } = new();
    }
}
