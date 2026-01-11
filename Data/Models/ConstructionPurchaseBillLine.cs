using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class ConstructionPurchaseBillLine
    {
        public int ConstructionPurchaseBillLineId { get; set; }
        public int CompanyId { get; set; }

        public int ConstructionPurchaseBillId { get; set; }
        public ConstructionPurchaseBill? ConstructionPurchaseBill { get; set; }

        public int ItemId { get; set; }

        [MaxLength(200)]
        public string ItemName { get; set; } = "";

        public decimal Qty { get; set; }
        public decimal Rate { get; set; }

        // store as percent: 5 = 5%
        public decimal VatRate { get; set; } = 5m;

        public decimal LineNet { get; set; }
        public decimal LineVat { get; set; }
        public decimal LineTotal { get; set; }
    }
}
