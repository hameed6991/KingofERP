using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public class LedgerService
{
    private readonly AppDbContext _db;

    public LedgerService(AppDbContext db)
    {
        _db = db;
    }

    // =========================
    // RESULT MODELS
    // =========================

    private static string BuildRefNo(string voucherType, string voucherNo)
    {
        voucherType = (voucherType ?? "").Trim().ToUpperInvariant();
        voucherNo = (voucherNo ?? "").Trim();

        if (voucherType == "INV") return voucherNo;
        if (voucherType == "JV") return voucherNo;

        return $"{voucherType}-{voucherNo}";
    }

    public class LedgerResult
    {
        public string AccountName { get; set; } = "";
        public decimal OpeningBalance { get; set; }
        public List<LedgerRow> Rows { get; set; } = new();
    }

    public class LedgerRow
    {
        public DateTime Date { get; set; }
        public string Explanation { get; set; } = "";
        public string Ref { get; set; } = "";

        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }

        public string Party { get; set; } = "-";

        public string VoucherType { get; set; } = "";
        public string VoucherNo { get; set; } = "";
        public int? RefId { get; set; }
    }

    // =========================
    // ✅ SEED / ENSURE DEFAULT COA
    // =========================
    // ✅ One-time seed for new company (base accounts)
    // ✅ Always ensure PDC/Charges accounts exist (even for existing companies)
    public async Task EnsureDefaultAccountsAsync(int companyId)
    {
        if (companyId <= 0) throw new Exception("Invalid company.");

        // 1) If company has NO accounts at all => seed base COA
        var anyCoa = await _db.ChartOfAccounts.AnyAsync(x => x.CompanyId == companyId);
        if (!anyCoa)
        {
            var baseCoa = new List<ChartOfAccount>
            {
                new()
                {
                    CompanyId = companyId,
                    AccountNo = 1000,
                    AccountType = "Asset",
                    AccountName = "Cash",
                    FinancialStatement = "BalanceSheet",
                    CashFlowGroup = "Operating",
                    IsActive = true,
                    IsCashAccount = true,
                    IsNonCashExpense = false,
                    IsWorkingCapital = true
                },
                new()
                {
                    CompanyId = companyId,
                    AccountNo = 1100,
                    AccountType = "Asset",
                    AccountName = "Accounts Receivable",
                    FinancialStatement = "BalanceSheet",
                    CashFlowGroup = "Operating",
                    IsActive = true,
                    IsCashAccount = false,
                    IsNonCashExpense = false,
                    IsWorkingCapital = true
                },
                new()
                {
                    CompanyId = companyId,
                    AccountNo = 1200,
                    AccountType = "Asset",
                    AccountName = "Inventory",
                    FinancialStatement = "BalanceSheet",
                    CashFlowGroup = "Operating",
                    IsActive = true,
                    IsCashAccount = false,
                    IsNonCashExpense = false,
                    IsWorkingCapital = true
                },
                new()
                {
                    CompanyId = companyId,
                    AccountNo = 1500,
                    AccountType = "Asset",
                    AccountName = "VAT Input",
                    FinancialStatement = "BalanceSheet",
                    CashFlowGroup = "Operating",
                    IsActive = true,
                    IsCashAccount = false,
                    IsNonCashExpense = false,
                    IsWorkingCapital = true
                },

                new()
                {
                    CompanyId = companyId,
                    AccountNo = 2000,
                    AccountType = "Liability",
                    AccountName = "Accounts Payable",
                    FinancialStatement = "BalanceSheet",
                    CashFlowGroup = "Operating",
                    IsActive = true,
                    IsCashAccount = false,
                    IsNonCashExpense = false,
                    IsWorkingCapital = true
                },
                new()
                {
                    CompanyId = companyId,
                    AccountNo = 2100,
                    AccountType = "Liability",
                    AccountName = "VAT Output",
                    FinancialStatement = "BalanceSheet",
                    CashFlowGroup = "Operating",
                    IsActive = true,
                    IsCashAccount = false,
                    IsNonCashExpense = false,
                    IsWorkingCapital = true
                },

                new()
                {
                    CompanyId = companyId,
                    AccountNo = 4000,
                    AccountType = "Revenue",
                    AccountName = "Sales Revenue",
                    FinancialStatement = "IncomeStatement",
                    CashFlowGroup = "Operating",
                    IsActive = true,
                    IsCashAccount = false,
                    IsNonCashExpense = false,
                    IsWorkingCapital = false
                },
                new()
                {
                    CompanyId = companyId,
                    AccountNo = 5000,
                    AccountType = "Expense",
                    AccountName = "Cost of Goods Sold",
                    FinancialStatement = "IncomeStatement",
                    CashFlowGroup = "Operating",
                    IsActive = true,
                    IsCashAccount = false,
                    IsNonCashExpense = false,
                    IsWorkingCapital = false
                },
            };

            _db.ChartOfAccounts.AddRange(baseCoa);
        }

        // 2) ✅ Always ensure these 3 accounts exist for Cheque/PDC module
        await EnsureAccountAsync(
            companyId,
            preferredAccountNo: 1300,
            name: "PDC Receivable / Cheques-in-hand",
            accountType: "Asset",
            financialStatement: "BalanceSheet",
            cashFlowGroup: "Operating",
            isCashAccount: false,
            isNonCashExpense: false,
            isWorkingCapital: true);

        await EnsureAccountAsync(
            companyId,
            preferredAccountNo: 2200,
            name: "PDC Payable Clearing",
            accountType: "Liability",
            financialStatement: "BalanceSheet",
            cashFlowGroup: "Operating",
            isCashAccount: false,
            isNonCashExpense: false,
            isWorkingCapital: true);

        await EnsureAccountAsync(
            companyId,
            preferredAccountNo: 5200,
            name: "Bank Charges",
            accountType: "Expense",
            financialStatement: "IncomeStatement",
            cashFlowGroup: "Operating",
            isCashAccount: false,
            isNonCashExpense: false,
            isWorkingCapital: false);

        await _db.SaveChangesAsync();
    }

    private async Task EnsureAccountAsync(
        int companyId,
        int preferredAccountNo,
        string name,
        string accountType,
        string financialStatement,
        string cashFlowGroup,
        bool isCashAccount = false,
        bool isNonCashExpense = false,
        bool isWorkingCapital = false)
    {
        // If same NAME already exists, do nothing
        var byName = await _db.ChartOfAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.AccountName == name);

        if (byName != null) return;

        // Find a free AccountNo starting from preferredAccountNo
        var usedNos = await _db.ChartOfAccounts
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.AccountNo)
            .ToListAsync();

        var chosen = preferredAccountNo;
        while (usedNos.Contains(chosen))
        {
            chosen++;
            if (chosen > 9999)
                throw new Exception("Cannot auto-create COA account. No free AccountNo available.");
        }

        _db.ChartOfAccounts.Add(new ChartOfAccount
        {
            CompanyId = companyId,
            AccountNo = chosen,
            AccountName = name,
            AccountType = accountType,
            FinancialStatement = financialStatement,
            CashFlowGroup = cashFlowGroup,
            IsActive = true,
            IsCashAccount = isCashAccount,
            IsNonCashExpense = isNonCashExpense,
            IsWorkingCapital = isWorkingCapital
        });
    }

    // =========================
    // ✅ POST (SINGLE ENTRY)
    // =========================
    public async Task PostAsync(
        int companyId,
        DateTime date,
        string voucherType,
        string voucherNo,
        int debitAccountNo,
        int creditAccountNo,
        decimal amount,
        string? narration = null,
        int? refId = null)
    {
        if (companyId <= 0) throw new Exception("Invalid company.");
        if (amount <= 0) throw new Exception("Amount must be > 0.");
        if (debitAccountNo <= 0 || creditAccountNo <= 0) throw new Exception("Invalid account.");
        if (debitAccountNo == creditAccountNo) throw new Exception("Debit & Credit account cannot be same.");

        voucherType = (voucherType ?? "").Trim().ToUpperInvariant();
        voucherNo = (voucherNo ?? "").Trim();
        var txnDate = date.Date;

        var gle = new GeneralLedgerEntry
        {
            CompanyId = companyId,
            TxnDate = txnDate,
            VoucherType = voucherType,
            VoucherNo = voucherNo,
            RefId = refId,
            DebitAccountNo = debitAccountNo,
            CreditAccountNo = creditAccountNo,
            Amount = amount,
            Narration = narration
        };
        _db.GeneralLedgerEntries.Add(gle);

        var refNo = BuildRefNo(voucherType, voucherNo);

        var dr = new LedgerEntry
        {
            CompanyId = companyId,
            TxnDate = txnDate,
            AccountNo = debitAccountNo,
            Debit = amount,
            Credit = 0,
            RefNo = refNo,
            Narration = narration
        };

        var cr = new LedgerEntry
        {
            CompanyId = companyId,
            TxnDate = txnDate,
            AccountNo = creditAccountNo,
            Debit = 0,
            Credit = amount,
            RefNo = refNo,
            Narration = narration
        };

        _db.LedgerEntries.AddRange(dr, cr);

        await _db.SaveChangesAsync();
    }

    // =========================
    // ✅ POST MANY LINES (1 TRANSACTION)
    // =========================
    public class PostLine
    {
        public int DebitAccountNo { get; set; }
        public int CreditAccountNo { get; set; }
        public decimal Amount { get; set; }
        public string? Narration { get; set; }
    }

    public async Task PostManyAsync(
        int companyId,
        DateTime date,
        string voucherType,
        string voucherNo,
        IEnumerable<PostLine> lines,
        int? refId = null)
    {
        if (companyId <= 0) throw new Exception("Invalid company.");

        voucherType = (voucherType ?? "").Trim().ToUpperInvariant();
        voucherNo = (voucherNo ?? "").Trim();
        var txnDate = date.Date;

        var list = (lines ?? Enumerable.Empty<PostLine>())
            .Where(x => x != null && x.Amount > 0)
            .ToList();

        if (list.Count == 0) throw new Exception("No lines to post.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            foreach (var ln in list)
            {
                if (ln.DebitAccountNo <= 0 || ln.CreditAccountNo <= 0)
                    throw new Exception("Invalid account in posting line.");
                if (ln.DebitAccountNo == ln.CreditAccountNo)
                    throw new Exception("Debit & Credit account cannot be same.");

                _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
                {
                    CompanyId = companyId,
                    TxnDate = txnDate,
                    VoucherType = voucherType,
                    VoucherNo = voucherNo,
                    RefId = refId,
                    DebitAccountNo = ln.DebitAccountNo,
                    CreditAccountNo = ln.CreditAccountNo,
                    Amount = ln.Amount,
                    Narration = ln.Narration
                });

                var refNo = BuildRefNo(voucherType, voucherNo);

                _db.LedgerEntries.Add(new LedgerEntry
                {
                    CompanyId = companyId,
                    TxnDate = txnDate,
                    AccountNo = ln.DebitAccountNo,
                    Debit = ln.Amount,
                    Credit = 0,
                    RefNo = refNo,
                    Narration = ln.Narration
                });

                _db.LedgerEntries.Add(new LedgerEntry
                {
                    CompanyId = companyId,
                    TxnDate = txnDate,
                    AccountNo = ln.CreditAccountNo,
                    Debit = 0,
                    Credit = ln.Amount,
                    RefNo = refNo,
                    Narration = ln.Narration
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // =========================
    // BALANCE (FIXED + FALLBACK)
    // =========================
    public async Task<decimal> GetBalanceAsync(int companyId, int accountNo)
    {
        var hasGle = await _db.GeneralLedgerEntries.AsNoTracking()
            .AnyAsync(x => x.CompanyId == companyId
                        && (x.DebitAccountNo == accountNo || x.CreditAccountNo == accountNo));

        if (hasGle)
        {
            var debit = await _db.GeneralLedgerEntries.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.DebitAccountNo == accountNo)
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            var credit = await _db.GeneralLedgerEntries.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.CreditAccountNo == accountNo)
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            return debit - credit;
        }

        var dr2 = await _db.LedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.AccountNo == accountNo)
            .SumAsync(x => (decimal?)x.Debit) ?? 0m;

        var cr2 = await _db.LedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.AccountNo == accountNo)
            .SumAsync(x => (decimal?)x.Credit) ?? 0m;

        return dr2 - cr2;
    }

    // =========================
    // ✅ GENERAL LEDGER WITH PARTY
    // =========================
    public async Task<LedgerResult> GetLedgerAsync(int companyId, int accountNo, DateTime from, DateTime to)
    {
        from = from.Date;
        to = to.Date;

        var acc = await _db.ChartOfAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.AccountNo == accountNo);

        var accName = acc?.AccountName ?? $"Account {accountNo}";

        var hasGle = await _db.GeneralLedgerEntries.AsNoTracking()
            .AnyAsync(x => x.CompanyId == companyId
                        && (x.DebitAccountNo == accountNo || x.CreditAccountNo == accountNo));

        if (!hasGle)
        {
            var openingDr = await _db.LedgerEntries.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.TxnDate < from && x.AccountNo == accountNo)
                .SumAsync(x => (decimal?)x.Debit) ?? 0m;

            var openingCr = await _db.LedgerEntries.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.TxnDate < from && x.AccountNo == accountNo)
                .SumAsync(x => (decimal?)x.Credit) ?? 0m;

            var openingBal = openingDr - openingCr;

            var entries2 = await _db.LedgerEntries.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.AccountNo == accountNo
                         && x.TxnDate >= from && x.TxnDate <= to)
                .OrderBy(x => x.TxnDate)
                .ThenBy(x => x.RefNo)
                .ThenBy(x => x.Narration)
                .Select(x => new
                {
                    x.TxnDate,
                    x.RefNo,
                    x.Narration,
                    x.Debit,
                    x.Credit
                })
                .ToListAsync();

            var rows2 = new List<LedgerRow>();
            decimal bal2 = openingBal;

            foreach (var e in entries2)
            {
                bal2 += (e.Debit - e.Credit);

                rows2.Add(new LedgerRow
                {
                    Date = e.TxnDate.Date,
                    Explanation = string.IsNullOrWhiteSpace(e.Narration) ? (e.RefNo ?? "") : e.Narration.Trim(),
                    Ref = e.RefNo ?? "",
                    Debit = e.Debit,
                    Credit = e.Credit,
                    Balance = bal2,
                    Party = "-"
                });
            }

            return new LedgerResult
            {
                AccountName = accName,
                OpeningBalance = openingBal,
                Rows = rows2
            };
        }

        var openingDebit = await _db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.TxnDate < from && x.DebitAccountNo == accountNo)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var openingCredit = await _db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.TxnDate < from && x.CreditAccountNo == accountNo)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var opening = openingDebit - openingCredit;

        var entries = await _db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                     && x.TxnDate >= from && x.TxnDate <= to
                     && (x.DebitAccountNo == accountNo || x.CreditAccountNo == accountNo))
            .OrderBy(x => x.TxnDate)
            .ThenBy(x => x.GeneralLedgerEntryId)
            .Select(x => new
            {
                x.TxnDate,
                x.VoucherType,
                x.VoucherNo,
                x.RefId,
                x.Amount,
                x.Narration,
                IsDebit = (x.DebitAccountNo == accountNo)
            })
            .ToListAsync();

        var rows = new List<LedgerRow>();
        decimal bal = opening;

        foreach (var e in entries)
        {
            var dr = e.IsDebit ? e.Amount : 0m;
            var cr = e.IsDebit ? 0m : e.Amount;

            bal += (dr - cr);

            rows.Add(new LedgerRow
            {
                Date = e.TxnDate.Date,
                Explanation = string.IsNullOrWhiteSpace(e.Narration) ? $"{e.VoucherType} {e.VoucherNo}" : e.Narration.Trim(),
                Ref = $"{e.VoucherType}-{e.VoucherNo}",
                Debit = dr,
                Credit = cr,
                Balance = bal,

                VoucherType = e.VoucherType ?? "",
                VoucherNo = e.VoucherNo ?? "",
                RefId = e.RefId,
                Party = "-"
            });
        }

        // Party mapping only for AR/AP (kept as-is)
        if (accountNo == 1100)
        {
            var invNos = rows
                .Where(r => r.VoucherType == "INV" || r.VoucherType == "REC")
                .Select(r => r.VoucherNo)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var invIds = rows
                .Where(r => r.RefId.HasValue)
                .Select(r => r.RefId!.Value)
                .Distinct()
                .ToList();

            var invoices = await _db.Invoices.AsNoTracking()
                .Where(x => x.CompanyId == companyId
                         && (invNos.Contains(x.InvoiceNo) || invIds.Contains(x.InvoiceId)))
                .Select(x => new { x.InvoiceId, x.InvoiceNo, x.CustomerName })
                .ToListAsync();

            var mapByNo = invoices
                .Where(x => !string.IsNullOrWhiteSpace(x.InvoiceNo))
                .ToDictionary(x => x.InvoiceNo, x => x.CustomerName ?? "-");

            var mapById = invoices.ToDictionary(x => x.InvoiceId, x => x.CustomerName ?? "-");

            foreach (var r in rows)
            {
                if (r.VoucherType != "INV" && r.VoucherType != "REC") continue;

                if (r.RefId.HasValue && mapById.TryGetValue(r.RefId.Value, out var custById))
                    r.Party = custById;
                else if (!string.IsNullOrWhiteSpace(r.VoucherNo) && mapByNo.TryGetValue(r.VoucherNo, out var custByNo))
                    r.Party = custByNo;
            }
        }
        else if (accountNo == 2000)
        {
            var purNos = rows
                .Where(r => r.VoucherType == "PINV")
                .Select(r => r.VoucherNo)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var purIds = rows
                .Where(r => r.RefId.HasValue)
                .Select(r => r.RefId!.Value)
                .Distinct()
                .ToList();

            var purchases = await _db.PurchaseInvoices.AsNoTracking()
                .Where(x => x.CompanyId == companyId
                         && (purNos.Contains(x.PurchaseNo) || purIds.Contains(x.PurchaseInvoiceId)))
                .Select(x => new { x.PurchaseInvoiceId, x.PurchaseNo, x.VendorName })
                .ToListAsync();

            var mapByNo = purchases
                .Where(x => !string.IsNullOrWhiteSpace(x.PurchaseNo))
                .ToDictionary(x => x.PurchaseNo, x => x.VendorName ?? "-");

            var mapById = purchases.ToDictionary(x => x.PurchaseInvoiceId, x => x.VendorName ?? "-");

            foreach (var r in rows)
            {
                if (r.VoucherType != "PINV") continue;

                if (r.RefId.HasValue && mapById.TryGetValue(r.RefId.Value, out var venById))
                    r.Party = venById;
                else if (!string.IsNullOrWhiteSpace(r.VoucherNo) && mapByNo.TryGetValue(r.VoucherNo, out var venByNo))
                    r.Party = venByNo;
            }
        }

        return new LedgerResult
        {
            AccountName = accName,
            OpeningBalance = opening,
            Rows = rows
        };
    }

    // =========================
    // PROFIT & LOSS
    // =========================
    public class ProfitLossResult
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public decimal TotalRevenue { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal NetProfit => TotalRevenue - TotalExpense;

        public List<PlRow> Revenue { get; set; } = new();
        public List<PlRow> Expense { get; set; } = new();
    }

    public class PlRow
    {
        public int AccountNo { get; set; }
        public string AccountName { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public async Task<ProfitLossResult> GetProfitLossAsync(int companyId, DateTime from, DateTime to)
    {
        from = from.Date;
        to = to.Date;

        var coa = await _db.ChartOfAccounts.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                     && x.IsActive
                     && x.FinancialStatement == "IncomeStatement")
            .Select(x => new
            {
                x.AccountNo,
                x.AccountName,
                x.AccountType
            })
            .ToListAsync();

        var baseQ = _db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.TxnDate >= from && x.TxnDate <= to);

        var debitQ = baseQ.Select(x => new { AccountNo = x.DebitAccountNo, Debit = x.Amount, Credit = 0m });
        var creditQ = baseQ.Select(x => new { AccountNo = x.CreditAccountNo, Debit = 0m, Credit = x.Amount });

        var sums = await debitQ
            .Concat(creditQ)
            .GroupBy(x => x.AccountNo)
            .Select(g => new { AccountNo = g.Key, Debit = g.Sum(z => z.Debit), Credit = g.Sum(z => z.Credit) })
            .ToListAsync();

        (decimal debit, decimal credit) GetDrCr(int accNo)
        {
            var s = sums.FirstOrDefault(x => x.AccountNo == accNo);
            return s == null ? (0m, 0m) : (s.Debit, s.Credit);
        }

        var revRows = coa
            .Where(x => x.AccountType == "Revenue")
            .Select(x =>
            {
                var (dr, cr) = GetDrCr(x.AccountNo);
                var amt = cr - dr;
                return new PlRow { AccountNo = x.AccountNo, AccountName = x.AccountName, Amount = Math.Abs(amt) };
            })
            .Where(x => x.Amount != 0)
            .OrderBy(x => x.AccountNo)
            .ToList();

        var expRows = coa
            .Where(x => x.AccountType == "Expense")
            .Select(x =>
            {
                var (dr, cr) = GetDrCr(x.AccountNo);
                var amt = dr - cr;
                return new PlRow { AccountNo = x.AccountNo, AccountName = x.AccountName, Amount = Math.Abs(amt) };
            })
            .Where(x => x.Amount != 0)
            .OrderBy(x => x.AccountNo)
            .ToList();

        return new ProfitLossResult
        {
            From = from,
            To = to,
            Revenue = revRows,
            Expense = expRows,
            TotalRevenue = revRows.Sum(x => x.Amount),
            TotalExpense = expRows.Sum(x => x.Amount),
        };
    }
}
