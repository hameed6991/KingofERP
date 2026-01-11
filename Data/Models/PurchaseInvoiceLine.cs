using System.ComponentModel.DataAnnotations;


namespace UaeEInvoice.Data
{
    public class PurchaseInvoiceLine
    {
        public int PurchaseInvoiceLineId { get; set; }
        public int CompanyId { get; set; }

        public int PurchaseInvoiceId { get; set; }
        public PurchaseInvoice? PurchaseInvoice { get; set; }

        public int ItemId { get; set; }

        [MaxLength(200)]
        public string ItemName { get; set; } = "";

        public decimal Qty { get; set; }
        public decimal Rate { get; set; }

        public decimal VatRate { get; set; } = 5; // default 5%
        public decimal LineVat { get; set; }
        public decimal LineTotal { get; set; }
        public string? VendorInvoiceNo { get; set; }   // supplier bill reference no
        public decimal LineNet { get; set; }   // Qty * Rate (before VAT)


    }
}
