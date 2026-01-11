namespace UaeEInvoice.Data
{
    public class StockLedger
    {
        public int StockLedgerId { get; set; }

        public int CompanyId { get; set; }

        public DateTime TxnDate { get; set; } = DateTime.Today;

        public int ItemId { get; set; }
        public string ItemName { get; set; } = "";

        // + for IN, - for OUT
        public decimal QtyChange { get; set; }

        // Cost per unit (for valuation)
        public decimal UnitCost { get; set; }

        public string TxnType { get; set; } = "";  // "PURCHASE", "SALE", "ADJUST"
        public string RefNo { get; set; } = "";    // e.g., PI-000001 / INV-000001
        public string? Notes { get; set; }
    }
}
