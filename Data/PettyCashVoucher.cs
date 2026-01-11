using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class PettyCashVoucher
{
    public int PettyCashVoucherId { get; set; }
    public int CompanyId { get; set; }

    public int PettyCashClaimId { get; set; }

    [MaxLength(30)]
    public string VoucherNo { get; set; } = "";

    public DateTime VoucherDate { get; set; } = DateTime.Today;

    public int CashAccountNo { get; set; }  // Cash/Bank credit
    public string PaidTo { get; set; } = "";

    public PettyPaymentMethod Method { get; set; } = PettyPaymentMethod.Cash;

    public string? ReferenceNo { get; set; }
    public string? Notes { get; set; }

    public decimal Amount { get; set; } = 0m; // Total incl VAT
}
