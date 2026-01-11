using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Payroll;

public class PayrollService
{
    private readonly AppDbContext _db;

    public PayrollService(AppDbContext db)
    {
        _db = db;
    }

    // Cash/Bank accounts dropdown
    public async Task<List<ChartOfAccount>> GetCashBankAccountsAsync(int companyId)
    {
        return await _db.ChartOfAccounts.AsNoTracking()
  .Where(a => a.CompanyId == companyId
            && a.IsActive
            && (a.IsCashAccount
                || a.AccountName == "Cash"
                || a.AccountName == "Bank"))
            .OrderBy(a => a.AccountName)
            .ToListAsync();
    }

    // ------------------------------
    // APPROVE: Dr Salaries Expense / Cr Salaries Payable
    // ------------------------------
    public async Task ApproveAsync(int runId, int companyId)
    {
        var run = await _db.PayrollRuns
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.PayrollRunId == runId && r.CompanyId == companyId);

        if (run == null) throw new Exception("Payroll run not found.");

        if (run.Status == "Paid") throw new Exception("Already paid.");
        if (run.Status != "Draft") throw new Exception("Only Draft can be Approved.");

        if (run.Lines == null || run.Lines.Count == 0)
            throw new Exception("No payroll lines.");

        var totalNetPay = run.Lines.Sum(l => l.NetPay);
        if (totalNetPay <= 0) throw new Exception("Total NetPay must be > 0.");

        var salariesExpense = await GetAccountNoAsync(companyId, "Salaries Expense");
        var salariesPayable = await GetAccountNoAsync(companyId, "Salaries Payable");

        // Create GL Entry (VoucherType = PAY)
        _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
        {
            CompanyId = companyId,
            TxnDate = DateTime.Today,
            VoucherType = "PAY",
            VoucherNo = run.RunNo,
            RefId = run.PayrollRunId,

            DebitAccountNo = salariesExpense,
            CreditAccountNo = salariesPayable,

            Amount = totalNetPay
        });

        run.Status = "Approved";

        await _db.SaveChangesAsync();
    }

    // ------------------------------
    // PAY: Dr Salaries Payable / Cr Cash/Bank (selected)
    // ------------------------------
    public async Task PayAsync(int runId, int companyId, int cashBankAccountNo, DateTime paidOnDateTime)
    {
        var run = await _db.PayrollRuns
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.PayrollRunId == runId && r.CompanyId == companyId);

        if (run == null) throw new Exception("Payroll run not found.");

        if (run.Status == "Paid") throw new Exception("Already paid.");
        if (run.Status != "Approved") throw new Exception("Only Approved can be Paid.");

        if (run.Lines == null || run.Lines.Count == 0)
            throw new Exception("No payroll lines.");

        var totalNetPay = run.Lines.Sum(l => l.NetPay);
        if (totalNetPay <= 0) throw new Exception("Total NetPay must be > 0.");

        var salariesPayable = await GetAccountNoAsync(companyId, "Salaries Payable");

        // Create GL Entry (VoucherType = PPAY)
        _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
        {
            CompanyId = companyId,
            TxnDate = paidOnDateTime,
            VoucherType = "PPAY",
            VoucherNo = run.RunNo,
            RefId = run.PayrollRunId,

            DebitAccountNo = salariesPayable,
            CreditAccountNo = cashBankAccountNo,

            Amount = totalNetPay
        });

        run.Status = "Paid";

        await _db.SaveChangesAsync();
    }

    private async Task<int> GetAccountNoAsync(int companyId, string accountName)
    {
        var acc = await _db.ChartOfAccounts.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.IsActive && a.AccountName == accountName)
            .Select(a => (int?)a.AccountNo)
            .FirstOrDefaultAsync();

        if (acc == null)
            throw new Exception($"ChartOfAccounts-ல் '{accountName}' account இல்லை. First அதை create பண்ணுங்க.");

        return acc.Value;
    }
}
