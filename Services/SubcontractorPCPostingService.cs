using Microsoft.EntityFrameworkCore;
using System.Globalization;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public class SubcontractorPCPostingService
{
    private readonly AppDbContext _db;

    public SubcontractorPCPostingService(AppDbContext db)
    {
        _db = db;
    }

    // TODO: later move to Company configuration
    private const int AP_SUBCONTRACTOR = 2100;
    private const int VAT_INPUT = 1330;
    private const int SUBCONTRACT_WIP = 5105;        // Project Subcontract cost / WIP
    private const int RETENTION_PAYABLE = 2200;      // Retention liability
    private const int BACKCHARGE_RECOVERY = 4205;    // Recoveries/Other income (or contra cost)

    public async Task PostAsync(int companyId, int pcId)
    {
        var pc = await TryFindPc(companyId, pcId);
        if (pc == null) throw new Exception("Subcontractor PC not found.");

        var status = GetString(pc, "Status", "PcStatus") ?? "";
        if (status == "Posted") return;

        // Load lines (work sections)
        var lines = await TryLoadLines(pcId);

        // Calculate gross from lines (Current sum)
        var thisMonthGross = decimal.Round(lines.Sum(x => x.Current), 2);

        // Retention %
        var retRaw = GetDec(pc, "RetentionPct", "RetentionPercent", "RetentionPercentage", "RetentionRate");
        var retPct = (retRaw > 0 && retRaw <= 1) ? retRaw * 100m : retRaw;
        var retention = decimal.Round(thisMonthGross * (retPct / 100m), 2);

        // Backcharge / deductions
        var backcharge = decimal.Round(GetDec(pc, "BackchargeThisMonth", "Backcharge", "Deduction", "Deductions"), 2);

        // VAT %
        var vatRaw = GetDec(pc, "VatRate", "VATRate", "VatPercent", "VATPercent", "VatPct");
        var vatPct = (vatRaw > 0 && vatRaw <= 1) ? vatRaw * 100m : vatRaw;

        var payableExVat = decimal.Round(thisMonthGross - retention - backcharge, 2);
        if (payableExVat < 0) payableExVat = 0;

        var vat = decimal.Round(payableExVat * (vatPct / 100m), 2);
        var netPayable = decimal.Round(payableExVat + vat, 2);

        // Dates + references
        var pcNo = GetString(pc, "PCNo", "PcNo", "ClaimNo") ?? $"PC-{pcId}";
        var issue = GetDate(pc, "IssueDate", "PcDate", "ClaimDate", "Date") ?? DateTime.Today;

        // Vendor/Subcontractor Name (for narration)
        var subName = "";
        try
        {
            var sid = GetInt(pc, "SubcontractorId", "ConstructionSubcontractorId");
            if (sid > 0)
            {
                var sub = await _db.Set<ConstructionSubcontractor>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ConstructionSubcontractorId == sid);
                if (sub != null) subName = sub.Name;
            }
        }
        catch { /* ignore */ }

        var nar = $"PC {pcNo}" + (string.IsNullOrWhiteSpace(subName) ? "" : $" - {subName}");

        // Remove old GL (if repost)
        var old = await _db.GeneralLedgerEntries
            .Where(x => x.CompanyId == companyId && x.VoucherType == "PC" && x.RefId == pcId)
            .ToListAsync();
        if (old.Count > 0) _db.GeneralLedgerEntries.RemoveRange(old);

        if (thisMonthGross <= 0)
            throw new Exception("PC current gross is zero. Add lines with current values.");

        // 1) Cost/WIP Dr vs AP + Retention + Backcharge Cr
        // Dr WIP = gross
        // Cr AP = payableExVat
        // Cr Retention = retention
        // Cr Backcharge = backcharge

        if (payableExVat > 0)
        {
            _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                CompanyId = companyId,
                TxnDate = issue,
                VoucherType = "PC",
                VoucherNo = pcNo,
                RefId = pcId,
                DebitAccountNo = SUBCONTRACT_WIP,
                CreditAccountNo = AP_SUBCONTRACTOR,
                Amount = payableExVat,
                Narration = nar + " (Payable ex VAT)"
            });
        }

        if (retention > 0)
        {
            _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                CompanyId = companyId,
                TxnDate = issue,
                VoucherType = "PC",
                VoucherNo = pcNo,
                RefId = pcId,
                DebitAccountNo = SUBCONTRACT_WIP,
                CreditAccountNo = RETENTION_PAYABLE,
                Amount = retention,
                Narration = nar + " (Retention withheld)"
            });
        }

        if (backcharge > 0)
        {
            _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                CompanyId = companyId,
                TxnDate = issue,
                VoucherType = "PC",
                VoucherNo = pcNo,
                RefId = pcId,
                DebitAccountNo = SUBCONTRACT_WIP,
                CreditAccountNo = BACKCHARGE_RECOVERY,
                Amount = backcharge,
                Narration = nar + " (Backcharge / deductions)"
            });
        }

        // If no payableExVat (rare), still ensure gross posted somewhere
        if (payableExVat == 0 && retention == 0 && backcharge == 0)
        {
            // Dr WIP, Cr AP full gross (fallback)
            _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                CompanyId = companyId,
                TxnDate = issue,
                VoucherType = "PC",
                VoucherNo = pcNo,
                RefId = pcId,
                DebitAccountNo = SUBCONTRACT_WIP,
                CreditAccountNo = AP_SUBCONTRACTOR,
                Amount = thisMonthGross,
                Narration = nar + " (Gross)"
            });
        }

        // 2) VAT Dr Input VAT, Cr AP
        if (vat > 0)
        {
            _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                CompanyId = companyId,
                TxnDate = issue,
                VoucherType = "PC",
                VoucherNo = pcNo,
                RefId = pcId,
                DebitAccountNo = VAT_INPUT,
                CreditAccountNo = AP_SUBCONTRACTOR,
                Amount = vat,
                Narration = nar + " (VAT Input)"
            });
        }

        // Mark as posted (reflection safe)
        SetString(pc, "Status", "PcStatus", "Posted");
        SetDate(pc, "PostedAt", "PostDate", DateTime.UtcNow);
        SetDate(pc, "UpdatedAt", "ModifiedAt", DateTime.UtcNow);

        await _db.SaveChangesAsync();
    }

    // -------------------- Load helpers --------------------
    private async Task<object?> TryFindPc(int companyId, int id)
    {
        var set = _db.Set<SubcontractorPC>().AsNoTracking();

        foreach (var keyName in new[] { "SubcontractorPCId", "PCId", "PcId", "Id" })
        {
            try
            {
                return await set.FirstOrDefaultAsync(x =>
                    EF.Property<int>(x, "CompanyId") == companyId &&
                    EF.Property<int>(x, keyName) == id);
            }
            catch { }
        }
        return null;
    }

    private async Task<List<LineVm>> TryLoadLines(int pcId)
    {
        var set = _db.Set<SubcontractorPCLine>().AsNoTracking();

        foreach (var fk in new[] { "SubcontractorPCId", "PCId", "PcId" })
        {
            try
            {
                var rows = await set.Where(l => EF.Property<int>(l, fk) == pcId).ToListAsync();
                if (rows.Count > 0)
                    return rows.Select(MapLine).ToList();
            }
            catch { }
        }

        return new();
    }

    private static LineVm MapLine(object line)
    {
        var ws = GetString(line, "WorkSection", "Section", "Description", "Work") ?? "";
        var prev = GetDec(line, "PreviousAmount", "Previous", "Prev", "PrevAmount");
        var curr = GetDec(line, "CurrentAmount", "Current", "Curr", "CurrAmount");
        return new LineVm { WorkSection = ws, Previous = prev, Current = curr };
    }

    private sealed class LineVm
    {
        public string WorkSection { get; set; } = "";
        public decimal Previous { get; set; }
        public decimal Current { get; set; }
    }

    // -------------------- reflection helpers --------------------
    private static int GetInt(object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = obj.GetType().GetProperty(n);
            if (p == null) continue;
            var v = p.GetValue(obj);
            if (v == null) continue;
            if (v is int i) return i;
            if (int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out var x)) return x;
        }
        return 0;
    }

    private static decimal GetDec(object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = obj.GetType().GetProperty(n);
            if (p == null) continue;
            var v = p.GetValue(obj);
            if (v == null) continue;
            if (v is decimal d) return d;

            if (decimal.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var x))
                return x;
        }
        return 0m;
    }

    private static string? GetString(object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = obj.GetType().GetProperty(n);
            if (p == null) continue;
            var v = p.GetValue(obj);
            if (v == null) continue;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }
        return null;
    }

    private static DateTime? GetDate(object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = obj.GetType().GetProperty(n);
            if (p == null) continue;
            var v = p.GetValue(obj);
            if (v == null) continue;

            if (v is DateTime dt) return dt;

            var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
        }
        return null;
    }

    private static void SetString(object obj, string primaryName, string altName, string value)
    {
        foreach (var n in new[] { primaryName, altName })
        {
            var p = obj.GetType().GetProperty(n);
            if (p == null || !p.CanWrite) continue;
            if (p.PropertyType == typeof(string))
            {
                p.SetValue(obj, value);
                return;
            }
        }
    }

    private static void SetDate(object obj, string primaryName, string altName, DateTime value)
    {
        foreach (var n in new[] { primaryName, altName })
        {
            var p = obj.GetType().GetProperty(n);
            if (p == null || !p.CanWrite) continue;

            if (p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?))
            {
                p.SetValue(obj, value);
                return;
            }
        }
    }
}
