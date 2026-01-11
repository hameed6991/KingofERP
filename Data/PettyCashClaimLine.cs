using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class PettyCashClaimLine
    {
        public int PettyCashClaimLineId { get; set; }

        public int CompanyId { get; set; }

        public int PettyCashClaimId { get; set; }
        public PettyCashClaim? Claim { get; set; }

        [Required, MaxLength(200)]
        public string Description { get; set; } = "";

        public int ExpenseAccountNo { get; set; }

        [Range(0.01, 999999999)]
        public decimal Amount { get; set; }   // Net

        public decimal VatRate { get; set; } = 0.05m;
        public decimal VatAmount { get; set; } = 0m;

        [MaxLength(500)]
        public string? ReceiptPath { get; set; }
    }
}
