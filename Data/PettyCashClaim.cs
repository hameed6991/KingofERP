using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class PettyCashClaim
    {
        public int PettyCashClaimId { get; set; }
        public int CompanyId { get; set; }

        [MaxLength(30)]
        public string ClaimNo { get; set; } = "";

        public DateTime ClaimDate { get; set; } = DateTime.Today;

        [MaxLength(120)]
        public string RequestedBy { get; set; } = "";

        [MaxLength(120)]
        public string? Department { get; set; }

        [MaxLength(250)]
        public string? Purpose { get; set; }

        public PettyClaimStatus Status { get; set; } = PettyClaimStatus.Draft;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public decimal TotalAmount { get; set; }

        public int? VoucherId { get; set; }

        [MaxLength(30)]
        public string? VoucherNo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<PettyCashClaimLine> Lines { get; set; } = new List<PettyCashClaimLine>();
    }
}
