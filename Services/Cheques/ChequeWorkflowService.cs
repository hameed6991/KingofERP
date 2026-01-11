using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Cheques;

public class ChequeWorkflowService
{
    private readonly AppDbContext _db;

    public ChequeWorkflowService(AppDbContext db)
    {
        _db = db;
    }

    // -----------------------------
    // PUBLIC METHODS (used by UI)
    // -----------------------------

    public async Task MarkPrinted(int chequeTransactionId)
    {
        var c = await GetTxOrThrow(chequeTransactionId);

        if (c.Status != ChequeStatus.Draft)
            throw new Exception("Only Draft cheque can be marked as Printed.");

        c.Status = ChequeStatus.Printed;
        await _db.SaveChangesAsync();
    }

    public async Task MarkDepositedOrHandedOver(int chequeTransactionId)
    {
        var c = await GetTxOrThrow(chequeTransactionId);

        if (c.Status != ChequeStatus.Printed)
            throw new Exception("Only Printed cheque can be Deposited/HandedOver.");

        // Outgoing -> HandedOver, Incoming -> Deposited
        c.Status = (c.Direction == ChequeDirection.Outgoing)
            ? ChequeStatus.HandedOver
            : ChequeStatus.Deposited;

        // Stage-1 postings
        // Outgoing Normal: Dr AP  Cr Bank
        // Outgoing PDC   : Dr AP  Cr PDC Payable Clearing
        // Incoming Normal/PDC: Dr Cheques-in-hand (PDC Receivable) Cr AR  (recommended UAE)
        if (c.Direction == ChequeDirection.Outgoing)
        {
            if (c.Type == ChequeType.Normal)
            {
                await PostOnce(
                    companyId: c.CompanyId,
                    txnDate: c.ChequeDate.Date,
                    voucherNo: $"CHQ-{c.ChequeTransactionId}-S1",
                    refId: c.ChequeTransactionId,
                    dr: c.CounterAccountNo,    // AP
                    cr: c.BankAccountNo,       // Bank
                    amount: c.Amount
                );
            }
            else
            {
                await PostOnce(
                    companyId: c.CompanyId,
                    txnDate: c.ChequeDate.Date,
                    voucherNo: $"CHQ-{c.ChequeTransactionId}-S1",
                    refId: c.ChequeTransactionId,
                    dr: c.CounterAccountNo,    // AP
                    cr: c.ClearingAccountNo,   // PDC Payable Clearing
                    amount: c.Amount
                );
            }
        }
        else
        {
            // Incoming cheques: on deposit, move from AR to Cheques-in-hand (PDC Receivable)
            await PostOnce(
                companyId: c.CompanyId,
                txnDate: c.ChequeDate.Date,
                voucherNo: $"CHQ-{c.ChequeTransactionId}-S1",
                refId: c.ChequeTransactionId,
                dr: c.ClearingAccountNo,     // PDC Receivable / Cheques-in-hand
                cr: c.CounterAccountNo,      // AR
                amount: c.Amount
            );
        }

        await _db.SaveChangesAsync();
    }

    public async Task MarkCleared(int chequeTransactionId, DateTime clearDate)
    {
        var c = await GetTxOrThrow(chequeTransactionId);

        // allowed statuses
        if (c.Status != ChequeStatus.HandedOver &&
            c.Status != ChequeStatus.Deposited &&
            c.Status != ChequeStatus.Presented)
            throw new Exception("Cheque can be cleared only after HandedOver/Deposited/Presented.");

        c.ClearDate = clearDate.Date;
        c.Status = ChequeStatus.Cleared;

        // Stage-2 postings
        // Outgoing PDC: Dr Clearing Cr Bank
        // Incoming:    Dr Bank    Cr Clearing
        if (c.Direction == ChequeDirection.Outgoing)
        {
            if (c.Type == ChequeType.PDC)
            {
                await PostOnce(
                    companyId: c.CompanyId,
                    txnDate: clearDate.Date,
                    voucherNo: $"CHQ-{c.ChequeTransactionId}-S2",
                    refId: c.ChequeTransactionId,
                    dr: c.ClearingAccountNo,   // PDC Payable Clearing
                    cr: c.BankAccountNo,       // Bank
                    amount: c.Amount
                );
            }
            // Normal outgoing: already Bank posted in stage-1, so nothing extra
        }
        else
        {
            // Incoming cheque clear: Dr Bank Cr Cheques-in-hand
            await PostOnce(
                companyId: c.CompanyId,
                txnDate: clearDate.Date,
                voucherNo: $"CHQ-{c.ChequeTransactionId}-S2",
                refId: c.ChequeTransactionId,
                dr: c.BankAccountNo,
                cr: c.ClearingAccountNo,
                amount: c.Amount
            );
        }

        await _db.SaveChangesAsync();
    }

    public async Task MarkBounced(int chequeTransactionId, DateTime bounceDate, decimal bankChargeAmount)
    {
        var c = await GetTxOrThrow(chequeTransactionId);

        if (c.Status == ChequeStatus.Cleared || c.Status == ChequeStatus.Voided)
            throw new Exception("Cleared/Voided cheque cannot be bounced.");

        if (c.Status != ChequeStatus.HandedOver &&
            c.Status != ChequeStatus.Deposited &&
            c.Status != ChequeStatus.Presented)
            throw new Exception("Only HandedOver/Deposited/Presented cheque can be marked as Bounced.");

        c.Status = ChequeStatus.Bounced;
        c.ClearDate = bounceDate.Date;

        // Reverse Stage-1 posting (basic V1)
        // Outgoing Normal: reverse Dr Bank Cr AP
        // Outgoing PDC   : reverse Dr Clearing Cr AP
        // Incoming       : reverse Dr AR Cr Clearing
        if (c.Direction == ChequeDirection.Outgoing)
        {
            if (c.Type == ChequeType.Normal)
            {
                await PostOnce(
                    c.CompanyId,
                    bounceDate.Date,
                    $"CHQ-{c.ChequeTransactionId}-REV",
                    c.ChequeTransactionId,
                    dr: c.BankAccountNo,
                    cr: c.CounterAccountNo,
                    amount: c.Amount
                );
            }
            else
            {
                await PostOnce(
                    c.CompanyId,
                    bounceDate.Date,
                    $"CHQ-{c.ChequeTransactionId}-REV",
                    c.ChequeTransactionId,
                    dr: c.ClearingAccountNo,
                    cr: c.CounterAccountNo,
                    amount: c.Amount
                );
            }
        }
        else
        {
            await PostOnce(
                c.CompanyId,
                bounceDate.Date,
                $"CHQ-{c.ChequeTransactionId}-REV",
                c.ChequeTransactionId,
                dr: c.CounterAccountNo,     // AR back
                cr: c.ClearingAccountNo,
                amount: c.Amount
            );
        }

        // Optional: bank charges (if you mapped Bank Charges role into c.CounterAccountNo etc)
        // Here we just post Dr BankCharges Cr Bank if amount > 0 and account is available.
        if (bankChargeAmount > 0)
        {
            // You MUST set BankChargesAccountNo somewhere (role mapping) before using this.
            // If you don’t have it in ChequeTransaction, keep this disabled.
            // Example (IF you have c.BankChargesAccountNo):
            // await PostOnce(c.CompanyId, bounceDate.Date, $"CHQ-{c.ChequeTransactionId}-CHG", c.ChequeTransactionId,
            //     dr: c.BankChargesAccountNo, cr: c.BankAccountNo, amount: bankChargeAmount);
        }

        await _db.SaveChangesAsync();
    }

    public async Task VoidCheque(int chequeTransactionId, string reason)
    {
        var c = await GetTxOrThrow(chequeTransactionId);

        if (c.Status == ChequeStatus.Cleared)
            throw new Exception("Cleared cheque cannot be voided.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new Exception("Void reason is required.");

        // If already posted stage-1 (Printed -> HandedOver/Deposited), reverse like bounce (without charges)
        if (c.Status == ChequeStatus.HandedOver || c.Status == ChequeStatus.Deposited || c.Status == ChequeStatus.Presented)
        {
            // reuse same reverse rules
            if (c.Direction == ChequeDirection.Outgoing)
            {
                if (c.Type == ChequeType.Normal)
                {
                    await PostOnce(c.CompanyId, DateTime.Today, $"CHQ-{c.ChequeTransactionId}-VOIDREV", c.ChequeTransactionId,
                        dr: c.BankAccountNo, cr: c.CounterAccountNo, amount: c.Amount);
                }
                else
                {
                    await PostOnce(c.CompanyId, DateTime.Today, $"CHQ-{c.ChequeTransactionId}-VOIDREV", c.ChequeTransactionId,
                        dr: c.ClearingAccountNo, cr: c.CounterAccountNo, amount: c.Amount);
                }
            }
            else
            {
                await PostOnce(c.CompanyId, DateTime.Today, $"CHQ-{c.ChequeTransactionId}-VOIDREV", c.ChequeTransactionId,
                    dr: c.CounterAccountNo, cr: c.ClearingAccountNo, amount: c.Amount);
            }
        }

        c.Status = ChequeStatus.Voided;
        c.Notes = $"{(c.Notes ?? "")}\nVOID: {reason}".Trim();

        await _db.SaveChangesAsync();
    }

    // -----------------------------
    // HELPERS
    // -----------------------------

    private async Task<ChequeTransaction> GetTxOrThrow(int id)
    {
        var tx = await _db.Set<ChequeTransaction>()
            .FirstOrDefaultAsync(x => x.ChequeTransactionId == id);

        if (tx == null)
            throw new Exception("Cheque transaction not found.");

        return tx;
    }

    /// <summary>
    /// Posts a single double-entry row to GeneralLedgerEntry, only once per voucherNo.
    /// </summary>
    private async Task PostOnce(int companyId, DateTime txnDate, string voucherNo, int refId, int dr, int cr, decimal amount)
    {
        if (amount <= 0) throw new Exception("Amount must be > 0.");
        if (dr <= 0 || cr <= 0) throw new Exception("Invalid GL account mapping.");

        var exists = await _db.Set<GeneralLedgerEntry>()
            .AsNoTracking()
            .AnyAsync(x => x.CompanyId == companyId && x.VoucherType == "CHQ" && x.VoucherNo == voucherNo);

        if (exists) return; // prevent duplicate posting

        var gl = new GeneralLedgerEntry
        {
            CompanyId = companyId,
            TxnDate = txnDate.Date,
            VoucherType = "CHQ",
            VoucherNo = voucherNo,
            RefId = refId,
            DebitAccountNo = dr,
            CreditAccountNo = cr,
            Amount = amount
        };

        _db.Set<GeneralLedgerEntry>().Add(gl);
        await _db.SaveChangesAsync();
    }
}
