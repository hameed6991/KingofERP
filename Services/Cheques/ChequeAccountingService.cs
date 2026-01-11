using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Services.Cheques;

public class ChequeAccountingService
{
    private readonly AppDbContext _db;
    private readonly LedgerService _ledger;

    public ChequeAccountingService(AppDbContext db, LedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    // =========================================================
    // STEP 4: MUST-HAVE METHODS (V1)
    // =========================================================

    public async Task<int> CreateOutgoingChequeForBill(int purchaseInvoiceId, DateTime chequeDate, decimal amount, int chequeBookId)
    {
        if (amount <= 0) throw new Exception("Amount must be > 0.");

        var bill = await _db.Set<PurchaseInvoice>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PurchaseInvoiceId == purchaseInvoiceId);

        if (bill == null) throw new Exception("Purchase invoice not found.");

        var companyId = bill.CompanyId;
        if (companyId <= 0) throw new Exception("Invalid CompanyId.");

        var book = await GetBookOrThrow(companyId, chequeBookId);

        // Type: future date => PDC (UAE standard)
        var type = chequeDate.Date > DateTime.Today ? ChequeType.PDC : ChequeType.Normal;

        // Role accounts
        var ap = await GetRoleAccountNoOrThrow(companyId, AccountRoleKeys.AP, "Map Accounts Payable role (AP) first.");
        var clearing = type == ChequeType.PDC
            ? await GetRoleAccountNoOrThrow(companyId, AccountRoleKeys.PDC_PAYABLE_CLEARING, "Map PDC Payable Clearing role first.")
            : 0;

        // Cheque No from book
        var chequeNo = await TakeNextChequeNo(book);

        var tx = new ChequeTransaction
        {
            CompanyId = companyId,
            Direction = ChequeDirection.Outgoing,
            Type = type,
            Status = ChequeStatus.Draft,

            ChequeBookId = book.ChequeBookId,
            ChequeNo = chequeNo,

            IssueDate = DateTime.Today,
            ChequeDate = chequeDate.Date,
            Amount = amount,

            VendorId = bill.VendorId,
            PayeeName = (bill.VendorName ?? "").Trim(),

            PurchaseInvoiceId = bill.PurchaseInvoiceId,

            BankAccountNo = book.BankAccountNo,
            ClearingAccountNo = clearing,
            CounterAccountNo = ap,

            CreatedBy = "system",
            CreatedOn = DateTime.UtcNow
        };

        _db.Set<ChequeTransaction>().Add(tx);
        await _db.SaveChangesAsync();

        return tx.ChequeTransactionId;
    }

    public async Task<int> CreateIncomingChequeForInvoice(int invoiceId, DateTime chequeDate, decimal amount, int chequeBookId)
    {
        if (amount <= 0) throw new Exception("Amount must be > 0.");

        var inv = await _db.Set<Invoice>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);

        if (inv == null) throw new Exception("Invoice not found.");

        var companyId = inv.CompanyId;
        if (companyId <= 0) throw new Exception("Invalid CompanyId.");

        var book = await GetBookOrThrow(companyId, chequeBookId);

        var type = chequeDate.Date > DateTime.Today ? ChequeType.PDC : ChequeType.Normal;

        var ar = await GetRoleAccountNoOrThrow(companyId, AccountRoleKeys.AR, "Map Accounts Receivable role (AR) first.");
        var recvClearing = await GetRoleAccountNoOrThrow(companyId, AccountRoleKeys.PDC_RECEIVABLE, "Map PDC Receivable / Cheques-in-hand role first.");

        var chequeNo = await TakeNextChequeNo(book);

        var tx = new ChequeTransaction
        {
            CompanyId = companyId,
            Direction = ChequeDirection.Incoming,
            Type = type,
            Status = ChequeStatus.Draft,

            ChequeBookId = book.ChequeBookId,
            ChequeNo = chequeNo,

            IssueDate = DateTime.Today,
            ChequeDate = chequeDate.Date,
            Amount = amount,

            CustomerId = inv.CustomerId,
            PayeeName = (inv.CustomerName ?? "").Trim(),

            InvoiceId = inv.InvoiceId,

            BankAccountNo = book.BankAccountNo,
            ClearingAccountNo = recvClearing, // Cheques-in-hand
            CounterAccountNo = ar,            // AR

            CreatedBy = "system",
            CreatedOn = DateTime.UtcNow
        };

        _db.Set<ChequeTransaction>().Add(tx);
        await _db.SaveChangesAsync();

        return tx.ChequeTransactionId;
    }

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

        // outgoing => handed over, incoming => deposited
        c.Status = c.Direction == ChequeDirection.Outgoing ? ChequeStatus.HandedOver : ChequeStatus.Deposited;

        // ===== Stage-1 Posting =====
        // Outgoing Normal: Dr AP              Cr Bank
        // Outgoing PDC   : Dr AP              Cr PDC Payable Clearing
        // Incoming       : Dr Cheques-in-hand Cr AR
        if (c.Direction == ChequeDirection.Outgoing)
        {
            if (c.Type == ChequeType.Normal)
            {
                await PostOnce(
                    companyId: c.CompanyId,
                    txnDate: c.ChequeDate,
                    voucherNo: $"CHQ-{c.ChequeTransactionId}-S1",
                    refId: c.ChequeTransactionId,
                    dr: c.CounterAccountNo,     // AP
                    cr: c.BankAccountNo,        // Bank
                    amount: c.Amount,
                    narration: $"Outgoing cheque {c.ChequeNo} handed over"
                );
            }
            else
            {
                EnsureAccounts(c, needClearing: true);

                await PostOnce(
                    companyId: c.CompanyId,
                    txnDate: c.ChequeDate,
                    voucherNo: $"CHQ-{c.ChequeTransactionId}-S1",
                    refId: c.ChequeTransactionId,
                    dr: c.CounterAccountNo,     // AP
                    cr: c.ClearingAccountNo,    // PDC Payable Clearing
                    amount: c.Amount,
                    narration: $"Outgoing PDC {c.ChequeNo} handed over"
                );
            }
        }
        else
        {
            EnsureAccounts(c, needClearing: true);

            await PostOnce(
                companyId: c.CompanyId,
                txnDate: c.ChequeDate,
                voucherNo: $"CHQ-{c.ChequeTransactionId}-S1",
                refId: c.ChequeTransactionId,
                dr: c.ClearingAccountNo,      // Cheques-in-hand
                cr: c.CounterAccountNo,       // AR
                amount: c.Amount,
                narration: $"Incoming cheque {c.ChequeNo} deposited"
            );
        }

        await _db.SaveChangesAsync();
    }

    public async Task MarkCleared(int chequeTransactionId, DateTime clearDate)
    {
        var c = await GetTxOrThrow(chequeTransactionId);

        if (c.Status != ChequeStatus.HandedOver &&
            c.Status != ChequeStatus.Deposited &&
            c.Status != ChequeStatus.Presented)
            throw new Exception("Cheque can be cleared only after HandedOver/Deposited/Presented.");

        c.ClearDate = clearDate.Date;
        c.Status = ChequeStatus.Cleared;

        // ===== Stage-2 Posting =====
        // Outgoing PDC: Dr PDC Payable Clearing  Cr Bank
        // Incoming   : Dr Bank                  Cr Cheques-in-hand
        if (c.Direction == ChequeDirection.Outgoing)
        {
            if (c.Type == ChequeType.PDC)
            {
                EnsureAccounts(c, needClearing: true);

                await PostOnce(
                    companyId: c.CompanyId,
                    txnDate: clearDate.Date,
                    voucherNo: $"CHQ-{c.ChequeTransactionId}-S2",
                    refId: c.ChequeTransactionId,
                    dr: c.ClearingAccountNo,
                    cr: c.BankAccountNo,
                    amount: c.Amount,
                    narration: $"Outgoing PDC {c.ChequeNo} cleared"
                );
            }
            // Normal outgoing already hit Bank in stage-1, so no stage-2.
        }
        else
        {
            EnsureAccounts(c, needClearing: true);

            await PostOnce(
                companyId: c.CompanyId,
                txnDate: clearDate.Date,
                voucherNo: $"CHQ-{c.ChequeTransactionId}-S2",
                refId: c.ChequeTransactionId,
                dr: c.BankAccountNo,
                cr: c.ClearingAccountNo,
                amount: c.Amount,
                narration: $"Incoming cheque {c.ChequeNo} cleared"
            );
        }

        await _db.SaveChangesAsync();
    }

    public async Task MarkBounced(int chequeTransactionId, DateTime bounceDate, decimal bankChargeAmount)
    {
        var c = await GetTxOrThrow(chequeTransactionId);

        if (c.Status == ChequeStatus.Cleared)
            throw new Exception("Cleared cheque cannot be bounced. Use reversal journal if needed.");

        if (c.Status != ChequeStatus.HandedOver &&
            c.Status != ChequeStatus.Deposited &&
            c.Status != ChequeStatus.Presented)
            throw new Exception("Only HandedOver/Deposited/Presented cheque can be marked as Bounced.");

        c.Status = ChequeStatus.Bounced;
        c.ClearDate = bounceDate.Date;

        // Reverse Stage-1
        // Outgoing Normal: reverse Dr Bank     Cr AP
        // Outgoing PDC   : reverse Dr Clearing Cr AP
        // Incoming       : reverse Dr AR       Cr Clearing
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
                    amount: c.Amount,
                    narration: $"Outgoing cheque {c.ChequeNo} bounced (reverse)"
                );
            }
            else
            {
                EnsureAccounts(c, needClearing: true);

                await PostOnce(
                    c.CompanyId,
                    bounceDate.Date,
                    $"CHQ-{c.ChequeTransactionId}-REV",
                    c.ChequeTransactionId,
                    dr: c.ClearingAccountNo,
                    cr: c.CounterAccountNo,
                    amount: c.Amount,
                    narration: $"Outgoing PDC {c.ChequeNo} bounced (reverse)"
                );
            }
        }
        else
        {
            EnsureAccounts(c, needClearing: true);

            await PostOnce(
                c.CompanyId,
                bounceDate.Date,
                $"CHQ-{c.ChequeTransactionId}-REV",
                c.ChequeTransactionId,
                dr: c.CounterAccountNo,
                cr: c.ClearingAccountNo,
                amount: c.Amount,
                narration: $"Incoming cheque {c.ChequeNo} bounced (reverse)"
            );
        }

        // Optional bank charge posting (Dr Bank Charges Cr Bank)
        if (bankChargeAmount > 0)
        {
            var bankCharges = await GetRoleAccountNoOrThrow(c.CompanyId, AccountRoleKeys.BANK_CHARGES, "Map Bank Charges role first.");

            await PostOnce(
                c.CompanyId,
                bounceDate.Date,
                $"CHQ-{c.ChequeTransactionId}-CHG",
                c.ChequeTransactionId,
                dr: bankCharges,
                cr: c.BankAccountNo,
                amount: bankChargeAmount,
                narration: $"Bank charges for bounced cheque {c.ChequeNo}"
            );
        }

        await _db.SaveChangesAsync();
    }

    public async Task VoidCheque(int chequeTransactionId, string reason)
    {
        var c = await GetTxOrThrow(chequeTransactionId);

        if (string.IsNullOrWhiteSpace(reason))
            throw new Exception("Void reason is required.");

        if (c.Status == ChequeStatus.Cleared)
            throw new Exception("Cleared cheque cannot be voided.");

        // If stage-1 already posted (handed over / deposited), reverse it
        if (c.Status == ChequeStatus.HandedOver || c.Status == ChequeStatus.Deposited || c.Status == ChequeStatus.Presented)
        {
            if (c.Direction == ChequeDirection.Outgoing)
            {
                if (c.Type == ChequeType.Normal)
                {
                    await PostOnce(
                        c.CompanyId,
                        DateTime.Today,
                        $"CHQ-{c.ChequeTransactionId}-VOIDREV",
                        c.ChequeTransactionId,
                        dr: c.BankAccountNo,
                        cr: c.CounterAccountNo,
                        amount: c.Amount,
                        narration: $"Void cheque {c.ChequeNo} (reverse)"
                    );
                }
                else
                {
                    EnsureAccounts(c, needClearing: true);

                    await PostOnce(
                        c.CompanyId,
                        DateTime.Today,
                        $"CHQ-{c.ChequeTransactionId}-VOIDREV",
                        c.ChequeTransactionId,
                        dr: c.ClearingAccountNo,
                        cr: c.CounterAccountNo,
                        amount: c.Amount,
                        narration: $"Void PDC {c.ChequeNo} (reverse)"
                    );
                }
            }
            else
            {
                EnsureAccounts(c, needClearing: true);

                await PostOnce(
                    c.CompanyId,
                    DateTime.Today,
                    $"CHQ-{c.ChequeTransactionId}-VOIDREV",
                    c.ChequeTransactionId,
                    dr: c.CounterAccountNo,
                    cr: c.ClearingAccountNo,
                    amount: c.Amount,
                    narration: $"Void incoming cheque {c.ChequeNo} (reverse)"
                );
            }
        }

        c.Status = ChequeStatus.Voided;
        c.Notes = $"{(c.Notes ?? "")}\nVOID: {reason}".Trim();

        await _db.SaveChangesAsync();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void EnsureAccounts(ChequeTransaction c, bool needClearing)
    {
        if (c.BankAccountNo <= 0) throw new Exception("BankAccountNo is not set for cheque.");
        if (c.CounterAccountNo <= 0) throw new Exception("CounterAccountNo is not set for cheque.");
        if (needClearing && c.ClearingAccountNo <= 0) throw new Exception("ClearingAccountNo is not set for cheque.");
    }

    private async Task<ChequeBook> GetBookOrThrow(int companyId, int chequeBookId)
    {
        var book = await _db.Set<ChequeBook>()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ChequeBookId == chequeBookId);

        if (book == null) throw new Exception("Cheque book not found.");
        if (!book.IsActive) throw new Exception("Cheque book is inactive.");
        if (book.BankAccountNo <= 0) throw new Exception("Cheque book BankAccountNo not configured.");

        return book;
    }

    private async Task<string> TakeNextChequeNo(ChequeBook book)
    {
        if (book.NextNo < book.StartNo) book.NextNo = book.StartNo;
        if (book.NextNo > book.EndNo) throw new Exception("ChequeBook range exhausted. Create new cheque book.");

        var no = book.NextNo.ToString();
        book.NextNo++;

        await _db.SaveChangesAsync();
        return no;
    }

    private async Task<ChequeTransaction> GetTxOrThrow(int id)
    {
        var tx = await _db.Set<ChequeTransaction>()
            .FirstOrDefaultAsync(x => x.ChequeTransactionId == id);

        if (tx == null) throw new Exception("Cheque transaction not found.");
        return tx;
    }

    private async Task<int> GetRoleAccountNoOrThrow(int companyId, string roleKey, string errorMessage)
    {
        // ✅ FIX: your PK is "Id", not "AccountRoleMapId"
        var map = await _db.Set<AccountRoleMap>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive && x.RoleKey == roleKey)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (map == null || map.AccountNo <= 0)
            throw new Exception(errorMessage);

        return map.AccountNo;
    }

    /// <summary>
    /// Posts using LedgerService, but prevents duplicates per voucherNo.
    /// </summary>
    private async Task PostOnce(
        int companyId,
        DateTime txnDate,
        string voucherNo,
        int refId,
        int dr,
        int cr,
        decimal amount,
        string? narration)
    {
        if (amount <= 0) throw new Exception("Amount must be > 0.");
        if (dr <= 0 || cr <= 0) throw new Exception("Invalid account mapping.");
        if (dr == cr) throw new Exception("Debit & Credit account cannot be same.");

        // prevent duplicate posting
        var exists = await _db.Set<GeneralLedgerEntry>()
            .AsNoTracking()
            .AnyAsync(x => x.CompanyId == companyId && x.VoucherType == "CHQ" && x.VoucherNo == voucherNo);

        if (exists) return;

        await _ledger.PostAsync(
            companyId: companyId,
            date: txnDate.Date,
            voucherType: "CHQ",
            voucherNo: voucherNo,
            debitAccountNo: dr,
            creditAccountNo: cr,
            amount: amount,
            narration: narration,
            refId: refId
        );
    }
}
