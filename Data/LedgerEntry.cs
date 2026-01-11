using System;
using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class LedgerEntry
    {
        public int Id { get; set; }              // ✅ Primary Key

        public int CompanyId { get; set; }

        public DateTime TxnDate { get; set; }

        public int AccountNo { get; set; }

        public decimal Debit { get; set; }

        public decimal Credit { get; set; }

        // ✅ Voucher / reference number (JV-202512-00001, EXP-202512-00001 ...)
        [MaxLength(30)]
        public string? RefNo { get; set; }

        // ✅ Narration / description
        [MaxLength(250)]
        public string? Narration { get; set; }

        public int? InvoiceId { get; set; }
        public int? PurchaseInvoiceId { get; set; }
    }
}
