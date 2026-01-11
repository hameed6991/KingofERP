using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class Invoice:ICompanyEntity
    {
        public int InvoiceId { get; set; }
        public int CompanyId { get; set; }

        [Required]
        public string InvoiceNo { get; set; } = "";

        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? CustomerTRN { get; set; }

        public DateTime? DueDate { get; set; }   // ✅ new (nullable)

        public decimal SubTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public int? SelectedTemplateId { get; set; }   // ✅ Invoice create dropdown selection
        public string? TemplateSnapshotJson { get; set; }  // ✅ optional (future: snapshot)


        public List<InvoiceLine> Lines { get; set; } = new();
    }
}
