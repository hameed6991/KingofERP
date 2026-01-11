using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Reports;

public class AgingService
{
    private readonly AppDbContext _db;

    public AgingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AgingResponseDto> GetARAgingAsync(int companyId, DateTime asOfDate, string? search = null)
    {
        if (companyId <= 0) throw new Exception("CompanyId invalid.");

        var asOf = asOfDate.Date;

        var invQry = _db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.InvoiceDate <= asOf);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            invQry = invQry.Where(i =>
                i.InvoiceNo.Contains(s) ||
                i.CustomerName.Contains(s) ||
                (i.CustomerTRN != null && i.CustomerTRN.Contains(s)));
        }

        var invoices = await invQry
            .Select(i => new
            {
                i.InvoiceId,
                i.InvoiceNo,
                i.CustomerId,
                i.CustomerName,
                i.CustomerTRN,
                i.InvoiceDate,
                DueDate = (DateTime?)(i.DueDate ?? i.InvoiceDate.AddDays(30)),
                i.GrandTotal
            })
            .ToListAsync();

        if (invoices.Count == 0)
            return new AgingResponseDto { AsOfDate = asOf, Rows = new List<AgingRowDto>() };

        var ids = invoices.Select(x => x.InvoiceId).ToList();

        // receipts up to asOf date
        var paidMap = await _db.InvoiceReceipts.AsNoTracking()
            .Where(r => ids.Contains(r.InvoiceId) && r.ReceiptDate <= asOf)
            .GroupBy(r => r.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Paid = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Paid);

        // last reminder (optional)
        var lastRem = await _db.ReminderLogs.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DocType == "INV" && ids.Contains(x.DocId))
            .GroupBy(x => x.DocId)
            .Select(g => new { DocId = g.Key, LastAt = g.Max(x => x.SentAt) })
            .ToDictionaryAsync(x => x.DocId, x => (DateTime?)x.LastAt);

        var rows = new List<AgingRowDto>();

        foreach (var inv in invoices)
        {
            paidMap.TryGetValue(inv.InvoiceId, out var paid);
            var outstanding = inv.GrandTotal - paid;
            if (outstanding <= 0.009m) continue;

            var due = (inv.DueDate ?? inv.InvoiceDate.AddDays(30)).Date;
            var days = (asOf - due).Days; // overdue days; negative => not due yet

            lastRem.TryGetValue(inv.InvoiceId, out var lr);

            rows.Add(new AgingRowDto
            {
                DocType = "INV",
                DocId = inv.InvoiceId,
                DocNo = inv.InvoiceNo,
                PartyName = inv.CustomerName,
                PartyTRN = inv.CustomerTRN,
                DocDate = inv.InvoiceDate.Date,
                DueDate = due,
                DaysOverdue = days,
                Total = inv.GrandTotal,
                Paid = paid,
                Outstanding = outstanding,
                LastReminderAt = lr
            });
        }

        rows = rows
            .OrderByDescending(r => r.DaysOverdue) // most overdue first
            .ThenByDescending(r => r.Outstanding)
            .ToList();

        return new AgingResponseDto
        {
            AsOfDate = asOf,
            Rows = rows,
            Summary = AgingSummaryDto.FromRows(rows)
        };
    }

    public async Task<AgingResponseDto> GetAPAgingAsync(int companyId, DateTime asOfDate, string? search = null)
    {
        if (companyId <= 0) throw new Exception("CompanyId invalid.");

        var asOf = asOfDate.Date;

        var pQry = _db.PurchaseInvoices.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.PurchaseDate <= asOf);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            // adjust fields if you use VendorName / SupplierName etc
            pQry = pQry.Where(p => p.PurchaseNo.Contains(s) || p.VendorName.Contains(s));
        }

        var pins = await pQry
            .Select(p => new
            {
                p.PurchaseInvoiceId,
                p.PurchaseNo,
                p.VendorName,
                p.PurchaseDate,
                DueDate = (DateTime?)(p.DueDate ?? p.PurchaseDate.AddDays(30)),
                p.GrandTotal
            })
            .ToListAsync();

        if (pins.Count == 0)
            return new AgingResponseDto { AsOfDate = asOf, Rows = new List<AgingRowDto>() };

        var ids = pins.Select(x => x.PurchaseInvoiceId).ToList();

        var paidMap = await _db.PurchasePayments.AsNoTracking()
            .Where(r => ids.Contains(r.PurchaseInvoiceId) && r.PaymentDate <= asOf)
            .GroupBy(r => r.PurchaseInvoiceId)
            .Select(g => new { Id = g.Key, Paid = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Id, x => x.Paid);

        var lastRem = await _db.ReminderLogs.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DocType == "PINV" && ids.Contains(x.DocId))
            .GroupBy(x => x.DocId)
            .Select(g => new { DocId = g.Key, LastAt = g.Max(x => x.SentAt) })
            .ToDictionaryAsync(x => x.DocId, x => (DateTime?)x.LastAt);

        var rows = new List<AgingRowDto>();

        foreach (var p in pins)
        {
            paidMap.TryGetValue(p.PurchaseInvoiceId, out var paid);
            var outstanding = p.GrandTotal - paid;
            if (outstanding <= 0.009m) continue;

            var due = (p.DueDate ?? p.PurchaseDate.AddDays(30)).Date;
            var days = (asOf - due).Days;

            lastRem.TryGetValue(p.PurchaseInvoiceId, out var lr);

            rows.Add(new AgingRowDto
            {
                DocType = "PINV",
                DocId = p.PurchaseInvoiceId,
                DocNo = p.PurchaseNo,
                PartyName = p.VendorName,
                PartyTRN = null,
                DocDate = p.PurchaseDate.Date,
                DueDate = due,
                DaysOverdue = days,
                Total = p.GrandTotal,
                Paid = paid,
                Outstanding = outstanding,
                LastReminderAt = lr
            });
        }

        rows = rows
            .OrderByDescending(r => r.DaysOverdue)
            .ThenByDescending(r => r.Outstanding)
            .ToList();

        return new AgingResponseDto
        {
            AsOfDate = asOf,
            Rows = rows,
            Summary = AgingSummaryDto.FromRows(rows)
        };
    }

    public async Task MarkReminderSentAsync(int companyId, string docType, int docId, string? channel = "Manual", string? note = null, string? sentTo = null)
    {
        if (companyId <= 0) throw new Exception("CompanyId invalid.");
        if (docId <= 0) throw new Exception("DocId invalid.");

        _db.ReminderLogs.Add(new ReminderLog
        {
            CompanyId = companyId,
            DocType = (docType ?? "").Trim().ToUpperInvariant(),
            DocId = docId,
            Channel = string.IsNullOrWhiteSpace(channel) ? "Manual" : channel.Trim(),
            Note = note,
            SentTo = sentTo,
            SentAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}

public class AgingResponseDto
{
    public DateTime AsOfDate { get; set; }
    public AgingSummaryDto Summary { get; set; } = new();
    public List<AgingRowDto> Rows { get; set; } = new();
}

public class AgingRowDto
{
    public string DocType { get; set; } = "INV";
    public int DocId { get; set; }
    public string DocNo { get; set; } = "";
    public string PartyName { get; set; } = "";
    public string? PartyTRN { get; set; }
    public DateTime DocDate { get; set; }
    public DateTime DueDate { get; set; }
    public int DaysOverdue { get; set; } // >0 overdue, 0 today, <0 not due
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Outstanding { get; set; }
    public DateTime? LastReminderAt { get; set; }

    // buckets
    public string Bucket
    {
        get
        {
            if (DaysOverdue <= 0) return "Not Due";
            if (DaysOverdue <= 30) return "0-30";
            if (DaysOverdue <= 60) return "31-60";
            if (DaysOverdue <= 90) return "61-90";
            return "90+";
        }
    }
}

public class AgingSummaryDto
{
    public decimal TotalOutstanding { get; set; }
    public decimal NotDue { get; set; }
    public decimal B0_30 { get; set; }
    public decimal B31_60 { get; set; }
    public decimal B61_90 { get; set; }
    public decimal B90Plus { get; set; }

    public static AgingSummaryDto FromRows(List<AgingRowDto> rows)
    {
        var s = new AgingSummaryDto();
        foreach (var r in rows)
        {
            s.TotalOutstanding += r.Outstanding;
            if (r.DaysOverdue <= 0) s.NotDue += r.Outstanding;
            else if (r.DaysOverdue <= 30) s.B0_30 += r.Outstanding;
            else if (r.DaysOverdue <= 60) s.B31_60 += r.Outstanding;
            else if (r.DaysOverdue <= 90) s.B61_90 += r.Outstanding;
            else s.B90Plus += r.Outstanding;
        }
        return s;
    }
}
