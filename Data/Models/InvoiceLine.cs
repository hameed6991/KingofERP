using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class InvoiceLine
    {
        public int InvoiceLineId { get; set; }

        public int InvoiceId { get; set; }
        public int CompanyId { get; set; }

        public int ItemId { get; set; }
        public string ItemName { get; set; } = "";

        public decimal Qty { get; set; } = 1m;
        public decimal Rate { get; set; } = 0m;

        public decimal VatRate { get; set; } = 0.05m;

        public decimal LineSubTotal { get; set; }
        public decimal LineVat { get; set; }
        public decimal LineTotal { get; set; }
    }
}
