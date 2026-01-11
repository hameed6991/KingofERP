using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public class PettyCashService
{
    private readonly AppDbContext _db;
    private readonly LedgerService _ledger;

    // You already have VAT Input = 1500 in COA seed
    private const int VatInputAccountNo = 1500;

    public PettyCashService(AppDbContext db, LedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    // =========================
    // LIST
    // =========================
    public async Task<List<PettyCashClaim>> GetClaimsAsync(int companyId, string? search, PettyClaimStatus? status)
    {
        var q = _db.PettyCashClaims.AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(x =>
                x.ClaimNo.Contains(search) ||
                x.RequestedBy.Contains(search) ||
                (x.Purpose ?? "").Contains(search));
        }

        if (status.HasValue)
            q = q.Where(x => x.Status == status.Value);

        return await q.OrderByDescending(x => x.PettyCashClaimId).ToListAsync();
    }

    public async Task<PettyCashClaim?> GetClaimAsync(int companyId, int claimId)
    {
        return await _db.PettyCashClaims.AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.PettyCashClaimId == claimId);
    }

    // =========================
    // CREATE DRAFT (SAVES CLAIM + LINES)
    // =========================
    public async Task<int> CreateDraftAsync(
        int companyId,
        DateTime claimDate,
        string requestedBy,
        string? purpose,
        List<PettyCashClaimLine> lines,
        string? department = null,
        string? notes = null)
    {
        if (companyId <= 0) throw new Exception("Invalid company.");
        if (string.IsNullOrWhiteSpace(requestedBy)) throw new Exception("Requested By is required.");
        if (lines == null || lines.Count == 0) throw new Exception("Add at least one line.");

        // Calculate total = SUM(Amount + VatAmount)
        foreach (var l in lines)
        {
            if (l.Amount <= 0) throw new Exception("Line amount must be > 0.");

            if (l.VatRate < 0) l.VatRate = 0;
            l.VatAmount = Math.Round(l.Amount * l.VatRate, 2);
        }

        var total = Math.Round(lines.Sum(x => x.Amount + x.VatAmount), 2);

        // Generate ClaimNo
        var count = await _db.PettyCashClaims.CountAsync(x => x.CompanyId == companyId);
        var claimNo = $"PCC-{(count + 1):00000}";

        var claim = new PettyCashClaim
        {
            CompanyId = companyId,
            ClaimNo = claimNo,
            ClaimDate = claimDate.Date,
            RequestedBy = requestedBy.Trim(),
            Department = string.IsNullOrWhiteSpace(department) ? null : department.Trim(),
            Purpose = string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = PettyClaimStatus.Draft,
            TotalAmount = total
        };

        _db.PettyCashClaims.Add(claim);
        await _db.SaveChangesAsync();

        // Attach lines
        foreach (var l in lines)
        {
            l.CompanyId = companyId;
            l.PettyCashClaimId = claim.PettyCashClaimId;
        }

        _db.PettyCashClaimLines.AddRange(lines);
        await _db.SaveChangesAsync();

        return claim.PettyCashClaimId;
    }

    // =========================
    // WORKFLOW
    // =========================
    public async Task SubmitAsync(int companyId, int claimId)
    {
        var c = await _db.PettyCashClaims.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.PettyCashClaimId == claimId);
        if (c == null) throw new Exception("Claim not found.");
        if (c.Status != PettyClaimStatus.Draft) throw new Exception("Only Draft can be submitted.");

        c.Status = PettyClaimStatus.Submitted;
        await _db.SaveChangesAsync();
    }

    public async Task ApproveAsync(int companyId, int claimId)
    {
        var c = await _db.PettyCashClaims.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.PettyCashClaimId == claimId);
        if (c == null) throw new Exception("Claim not found.");
        if (c.Status != PettyClaimStatus.Submitted) throw new Exception("Only Submitted can be approved.");

        c.Status = PettyClaimStatus.Approved;
        await _db.SaveChangesAsync();
    }

    public async Task RejectAsync(int companyId, int claimId)
    {
        var c = await _db.PettyCashClaims.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.PettyCashClaimId == claimId);
        if (c == null) throw new Exception("Claim not found.");
        if (c.Status == PettyClaimStatus.Vouchered) throw new Exception("Already vouchered.");

        c.Status = PettyClaimStatus.Rejected;
        await _db.SaveChangesAsync();
    }

    // =========================
    // CREATE + POST VOUCHER
    // =========================
    public async Task<PettyCashVoucher> CreateAndPostVoucherFromClaimAsync(
        int companyId,
        int claimId,
        int cashAccountNo,
        string paidTo,
        PettyPaymentMethod method,
        string? referenceNo,
        string? notes)
    {
        var claim = await _db.PettyCashClaims
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.PettyCashClaimId == claimId);

        if (claim == null) throw new Exception("Claim not found.");
        if (claim.Status != PettyClaimStatus.Approved) throw new Exception("Only Approved claims can be vouchered.");
        if (cashAccountNo <= 0) throw new Exception("Cash/Bank account required.");
        if (string.IsNullOrWhiteSpace(paidTo)) throw new Exception("Paid To is required.");

        if (claim.Lines == null || claim.Lines.Count == 0) throw new Exception("Claim has no lines.");
        if (claim.TotalAmount <= 0) throw new Exception("Claim total must be > 0.");

        // Generate VoucherNo
        var count = await _db.PettyCashVouchers.CountAsync(x => x.CompanyId == companyId);
        var voucherNo = $"PCV-{(count + 1):00000}";

        // Post to ledger:
        // For each line:
        //   DR Expense (Amount)
        //   DR VAT Input (VatAmount) if any
        //   CR Cash/Bank (same amount)
        foreach (var ln in claim.Lines)
        {
            if (ln.Amount <= 0) continue;

            await _ledger.PostAsync(
                companyId: companyId,
                date: DateTime.Today,
                voucherType: "PCV",
                voucherNo: voucherNo,
                debitAccountNo: ln.ExpenseAccountNo,
                creditAccountNo: cashAccountNo,
                amount: ln.Amount,
                narration: $"PettyCash: {claim.ClaimNo} - {ln.Description}",
                refId: claim.PettyCashClaimId
            );

            if (ln.VatAmount > 0)
            {
                await _ledger.PostAsync(
                    companyId: companyId,
                    date: DateTime.Today,
                    voucherType: "PCV",
                    voucherNo: voucherNo,
                    debitAccountNo: VatInputAccountNo,
                    creditAccountNo: cashAccountNo,
                    amount: ln.VatAmount,
                    narration: $"VAT Input: {claim.ClaimNo} - {ln.Description}",
                    refId: claim.PettyCashClaimId
                );
            }
        }

        var voucher = new PettyCashVoucher
        {
            CompanyId = companyId,
            PettyCashClaimId = claim.PettyCashClaimId,
            VoucherNo = voucherNo,
            VoucherDate = DateTime.Today,
            CashAccountNo = cashAccountNo,
            PaidTo = paidTo.Trim(),
            Method = method,
            ReferenceNo = referenceNo,
            Notes = notes,
            Amount = claim.TotalAmount
        };

        _db.PettyCashVouchers.Add(voucher);

        claim.Status = PettyClaimStatus.Vouchered;
        claim.VoucherNo = voucherNo;

        await _db.SaveChangesAsync();

        return voucher;
    }
}
