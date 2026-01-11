using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public class BankReconciliationService
{
    private readonly AppDbContext _db;
    private readonly LedgerService _ledger;

    public BankReconciliationService(AppDbContext db, LedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    // ---------------------------
    // 1) Import CSV
    // Expected columns (any order):
    // Date, Narration, Debit, Credit, Balance (optional)
    // ---------------------------
    public async Task<int> ImportCsvAsync(int companyId, int bankAccountId, string fileName, string csvText)
    {
        if (companyId <= 0) throw new Exception("Invalid company.");
        if (bankAccountId <= 0) throw new Exception("Select bank account.");
        if (string.IsNullOrWhiteSpace(csvText)) throw new Exception("CSV content is empty.");

        var bankAcc = await _db.BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.BankAccountId == bankAccountId && x.IsActive);
        if (bankAcc == null) throw new Exception("Bank account mapping not found.");
        if (bankAcc.GlAccountNo == null) throw new Exception("Map Bank GL AccountNo (GlAccountNo) in BankAccounts.");

        var import = new BankStatementImport
        {
            CompanyId = companyId,
            BankAccountId = bankAccountId,
            SourceFileName = string.IsNullOrWhiteSpace(fileName) ? "statement.csv" : fileName
        };

        var rows = ParseCsv(csvText);
        if (rows.Count == 0) throw new Exception("No rows found in CSV.");

        foreach (var r in rows)
        {
            var amount = r.Credit > 0 ? r.Credit : r.Debit;
            if (amount <= 0) continue;

            var dir = r.Credit > 0 ? BankTxnDirection.Credit : BankTxnDirection.Debit;
            var refToken = ExtractRefToken(r.Narration);

            import.Lines.Add(new BankStatementLine
            {
                CompanyId = companyId,
                BankAccountId = bankAccountId,
                TxnDate = r.Date.Date,
                Narration = (r.Narration ?? "").Trim(),
                Direction = dir,
                Amount = amount,
                RunningBalance = r.Balance,
                ExtractedRef = refToken,
                Status = BankLineStatus.Unmatched,
                MatchMethod = BankMatchMethod.None
            });
        }

        if (import.Lines.Count == 0) throw new Exception("CSV has no valid debit/credit rows.");

        import.FromDate = import.Lines.Min(x => x.TxnDate);
        import.ToDate = import.Lines.Max(x => x.TxnDate);

        _db.BankStatementImports.Add(import);
        await _db.SaveChangesAsync();

        await AutoSuggestMatchesAsync(companyId, bankAccountId, import.BankStatementImportId);
        return import.BankStatementImportId;
    }

    // ---------------------------
    // 2) Auto suggest matches
    // A) Reference token match (INV-00012 etc)
    // B) Amount + date window (±3 days) + direction
    // ---------------------------
    public async Task AutoSuggestMatchesAsync(int companyId, int bankAccountId, int? importId = null)
    {
        var bankAcc = await _db.BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.BankAccountId == bankAccountId && x.IsActive);
        if (bankAcc == null) throw new Exception("Bank account mapping not found.");
        var bankGl = bankAcc.GlAccountNo ?? throw new Exception("Map Bank GL AccountNo (GlAccountNo) in BankAccounts.");

        var q = _db.BankStatementLines
            .Where(x => x.CompanyId == companyId
                     && x.BankAccountId == bankAccountId
                     && (x.Status == BankLineStatus.Unmatched || x.Status == BankLineStatus.Suggested));

        if (importId.HasValue)
            q = q.Where(x => x.BankStatementImportId == importId.Value);

        var bankLines = await q.OrderByDescending(x => x.TxnDate)
                               .ThenByDescending(x => x.BankStatementLineId)
                               .ToListAsync();

        foreach (var bl in bankLines)
        {
            // reset
            bl.Status = BankLineStatus.Unmatched;
            bl.MatchedVoucherType = null;
            bl.MatchedVoucherNo = null;
            bl.MatchedRefId = null;
            bl.Confidence = null;
            bl.MatchMethod = BankMatchMethod.None;
            bl.MatchNotes = null;

            // A) ref token
            if (!string.IsNullOrWhiteSpace(bl.ExtractedRef))
            {
                var hit = await FindGleByReferenceAsync(companyId, bankGl, bl.ExtractedRef!, bl.Amount);
                if (hit != null)
                {
                    bl.Status = BankLineStatus.Suggested;
                    bl.MatchedVoucherType = hit.VoucherType;
                    bl.MatchedVoucherNo = hit.VoucherNo;
                    bl.MatchedRefId = hit.RefId;
                    bl.Confidence = 0.95m;
                    bl.MatchMethod = BankMatchMethod.Reference;
                    bl.MatchNotes = "Matched by reference token in narration.";
                    continue;
                }
            }

            // B) amount+date
            var hit2 = await FindGleByAmountDateAsync(companyId, bankGl, bl.TxnDate, bl.Direction, bl.Amount);
            if (hit2 != null)
            {
                bl.Status = BankLineStatus.Suggested;
                bl.MatchedVoucherType = hit2.VoucherType;
                bl.MatchedVoucherNo = hit2.VoucherNo;
                bl.MatchedRefId = hit2.RefId;
                bl.Confidence = 0.75m;
                bl.MatchMethod = BankMatchMethod.AmountDate;
                bl.MatchNotes = "Matched by amount + date window + bank account.";
                continue;
            }
        }

        await _db.SaveChangesAsync();
    }

    // ---------------------------
    // 3) Approve suggestion
    // ---------------------------
    public async Task ApproveMatchAsync(int companyId, int bankLineId)
    {
        var bl = await _db.BankStatementLines
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.BankStatementLineId == bankLineId);

        if (bl == null) throw new Exception("Bank line not found.");
        if (bl.Status != BankLineStatus.Suggested) throw new Exception("No suggested match to approve.");
        if (string.IsNullOrWhiteSpace(bl.MatchedVoucherType) || string.IsNullOrWhiteSpace(bl.MatchedVoucherNo))
            throw new Exception("Suggestion missing voucher info.");

        bl.Status = BankLineStatus.Reconciled;
        bl.MatchNotes = (bl.MatchNotes ?? "") + " | Approved";
        await _db.SaveChangesAsync();
    }

    // ---------------------------
    // 4) Ignore
    // ---------------------------
    public async Task IgnoreAsync(int companyId, int bankLineId, string? reason = null)
    {
        var bl = await _db.BankStatementLines
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.BankStatementLineId == bankLineId);

        if (bl == null) throw new Exception("Bank line not found.");

        bl.Status = BankLineStatus.Ignored;
        bl.MatchNotes = reason;
        bl.MatchMethod = BankMatchMethod.Manual;
        await _db.SaveChangesAsync();
    }

    // ---------------------------
    // 5) Auto Post (creates GL entry & reconciles)
    // ---------------------------
    public async Task AutoPostAsync(
        int companyId,
        int bankAccountId,
        int bankLineId,
        int contraAccountNo,
        string voucherType,
        string voucherNo,
        string narration,
        int? refId = null)
    {
        var bankAcc = await _db.BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.BankAccountId == bankAccountId && x.IsActive);
        if (bankAcc == null) throw new Exception("Bank account mapping not found.");

        var bankGl = bankAcc.GlAccountNo ?? throw new Exception("Map Bank GL AccountNo (GlAccountNo) in BankAccounts.");

        var bl = await _db.BankStatementLines
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.BankStatementLineId == bankLineId);

        if (bl == null) throw new Exception("Bank line not found.");
        if (bl.Status == BankLineStatus.Reconciled) throw new Exception("Already reconciled.");
        if (contraAccountNo <= 0) throw new Exception("Select contra account.");

        var amt = bl.Amount;
        if (amt <= 0) throw new Exception("Invalid amount.");

        var vType = (voucherType ?? "").Trim().ToUpperInvariant();
        var vNo = (voucherNo ?? "").Trim();

        if (string.IsNullOrWhiteSpace(vType)) throw new Exception("Voucher type required.");
        if (string.IsNullOrWhiteSpace(vNo)) throw new Exception("Voucher no required.");

        // Bank credit => money IN => Debit Bank, Credit Contra
        if (bl.Direction == BankTxnDirection.Credit)
        {
            await _ledger.PostAsync(
                companyId: companyId,
                date: bl.TxnDate,
                voucherType: vType,
                voucherNo: vNo,
                debitAccountNo: bankGl,
                creditAccountNo: contraAccountNo,
                amount: amt,
                narration: narration,
                refId: refId
            );
        }
        else
        {
            // Bank debit => money OUT => Debit Contra, Credit Bank
            await _ledger.PostAsync(
                companyId: companyId,
                date: bl.TxnDate,
                voucherType: vType,
                voucherNo: vNo,
                debitAccountNo: contraAccountNo,
                creditAccountNo: bankGl,
                amount: amt,
                narration: narration,
                refId: refId
            );
        }

        bl.Status = BankLineStatus.Reconciled;
        bl.MatchedVoucherType = vType;
        bl.MatchedVoucherNo = vNo;
        bl.MatchedRefId = refId;
        bl.MatchMethod = BankMatchMethod.AutoPosted;
        bl.Confidence = 1.00m;
        bl.MatchNotes = "Auto-posted and reconciled.";
        await _db.SaveChangesAsync();
    }

    // =========================
    // Helpers
    // =========================
    private sealed class CsvRow
    {
        public DateTime Date { get; set; }
        public string Narration { get; set; } = "";
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal? Balance { get; set; }
    }

    private static List<CsvRow> ParseCsv(string csv)
    {
        var rows = new List<CsvRow>();
        if (string.IsNullOrWhiteSpace(csv)) return rows;

        var lines = csv.Split('\n')
            .Select(x => x.Trim('\r'))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (lines.Count < 2) return rows;

        var header = SplitCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();

        int idxDate = header.FindIndex(x => x.Contains("date"));
        int idxNarr = header.FindIndex(x => x.Contains("narr") || x.Contains("desc") || x.Contains("particular"));
        int idxDebit = header.FindIndex(x => x.Contains("debit") || x == "dr");
        int idxCredit = header.FindIndex(x => x.Contains("credit") || x == "cr");
        int idxBal = header.FindIndex(x => x.Contains("bal"));

        for (int i = 1; i < lines.Count; i++)
        {
            var cols = SplitCsvLine(lines[i]);
            if (cols.Count == 0) continue;

            if (idxDate < 0 || idxDate >= cols.Count) continue;

            if (!TryParseDate(cols[idxDate], out var dt)) continue;

            string narr = (idxNarr >= 0 && idxNarr < cols.Count) ? cols[idxNarr] : "";
            decimal debit = (idxDebit >= 0 && idxDebit < cols.Count) ? ParseDec(cols[idxDebit]) : 0m;
            decimal credit = (idxCredit >= 0 && idxCredit < cols.Count) ? ParseDec(cols[idxCredit]) : 0m;
            decimal? bal = (idxBal >= 0 && idxBal < cols.Count) ? (decimal?)ParseDec(cols[idxBal]) : null;

            rows.Add(new CsvRow { Date = dt, Narration = narr, Debit = debit, Credit = credit, Balance = bal });
        }

        return rows;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        if (line == null) return result;

        bool inQuotes = false;
        var cur = "";

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(cur.Trim());
                cur = "";
                continue;
            }

            cur += ch;
        }

        result.Add(cur.Trim());
        return result;
    }

    private static bool TryParseDate(string s, out DateTime dt)
    {
        s = (s ?? "").Trim();
        var fmts = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "MM/dd/yyyy" };
        foreach (var f in fmts)
        {
            if (DateTime.TryParseExact(s, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return true;
        }
        return DateTime.TryParse(s, out dt);
    }

    private static decimal ParseDec(string s)
    {
        s = (s ?? "").Trim().Replace(",", "");
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out d)) return d;
        return 0m;
    }

    private static string? ExtractRefToken(string narration)
    {
        narration = narration ?? "";

        var rx = new Regex(@"\b(INV|PINV|PCV|JV|REC|PPAY)\s*[-/]\s*([0-9]{1,10})\b", RegexOptions.IgnoreCase);
        var m = rx.Match(narration);
        if (m.Success)
        {
            var type = m.Groups[1].Value.ToUpperInvariant();
            var no = m.Groups[2].Value;
            return $"{type}-{no}";
        }

        var rx2 = new Regex(@"\b(INV|PINV|PCV|JV|REC|PPAY)-([A-Za-z0-9]+)\b", RegexOptions.IgnoreCase);
        var m2 = rx2.Match(narration);
        if (m2.Success) return $"{m2.Groups[1].Value.ToUpperInvariant()}-{m2.Groups[2].Value}";

        return null;
    }

    private sealed class GleHit
    {
        public string VoucherType { get; set; } = "";
        public string VoucherNo { get; set; } = "";
        public int? RefId { get; set; }
    }

    private async Task<GleHit?> FindGleByReferenceAsync(int companyId, int bankGlAccountNo, string refToken, decimal amount)
    {
        var parts = refToken.Split('-', 2);
        if (parts.Length != 2) return null;

        var vType = parts[0].Trim().ToUpperInvariant();
        var vNo = parts[1].Trim();

        var hit = await _db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                     && x.VoucherType == vType
                     && x.VoucherNo == vNo
                     && x.Amount == amount
                     && (x.DebitAccountNo == bankGlAccountNo || x.CreditAccountNo == bankGlAccountNo))
            .OrderByDescending(x => x.TxnDate)
            .Select(x => new GleHit { VoucherType = x.VoucherType, VoucherNo = x.VoucherNo, RefId = x.RefId })
            .FirstOrDefaultAsync();

        return hit;
    }

    private async Task<GleHit?> FindGleByAmountDateAsync(int companyId, int bankGlAccountNo, DateTime date, BankTxnDirection dir, decimal amount)
    {
        var from = date.Date.AddDays(-3);
        var to = date.Date.AddDays(3);

        var q = _db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                     && x.TxnDate >= from && x.TxnDate <= to
                     && x.Amount == amount);

        q = (dir == BankTxnDirection.Credit)
            ? q.Where(x => x.DebitAccountNo == bankGlAccountNo)
            : q.Where(x => x.CreditAccountNo == bankGlAccountNo);

        return await q.OrderByDescending(x => x.TxnDate)
            .Select(x => new GleHit { VoucherType = x.VoucherType, VoucherNo = x.VoucherNo, RefId = x.RefId })
            .FirstOrDefaultAsync();
    }
}
