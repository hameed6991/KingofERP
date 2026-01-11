namespace UaeEInvoice.Data
{
    public class ConstructionAccountsMap
    {
        public int ConstructionAccountsMapId { get; set; }
        public int CompanyId { get; set; }

        // Required GL account numbers (match your COA)
        public int SubcontractCostAccountNo { get; set; } = 5001;
        public int AccountsPayableAccountNo { get; set; } = 2101;
        public int RetentionPayableAccountNo { get; set; } = 2102;
        public int VatInputAccountNo { get; set; } = 1203;
        public int BackchargeRecoveryAccountNo { get; set; } = 4201;

        public bool IsActive { get; set; } = true;
    }
}
