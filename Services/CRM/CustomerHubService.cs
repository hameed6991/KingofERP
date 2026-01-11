using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.CRM;

public class CustomerHubService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    public CustomerHubService(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    // ---------- Internal lite models (avoid anonymous/dynamic) ----------
    private sealed class InvoiceLite
    {
        public int InvoiceId { get; set; }
        public int CustomerId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal GrandTotal { get; set; }
    }

    private sealed class ReceiptLite
    {
        public int InvoiceId { get; set; }
        public DateTime TxnDate { get; set; }
        public decimal Amount { get; set; }
    }

    // ---------- DTOs ----------
    public sealed class CustomerHubRow
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = "";
        public string CustomerType { get; set; } = "";
        public string? TRN { get; set; }
        public string? City { get; set; }

        public decimal Outstanding { get; set; }
        public int OverdueCount { get; set; }
        public DateTime? LastInvoiceDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }

        public DateTime? NextFollowUpDate { get; set; }
        public string? NextFollowUpNote { get; set; }

        public int MaxOverdueDays { get; set; }
        public string Risk { get; set; } = "Green"; // Green/Amber/Red
    }

    public sealed class CustomerHubSummary
    {
        public int TotalCustomers { get; set; }
        public decimal TotalOutstanding { get; set; }
        public int TotalOverdueInvoices { get; set; }
        public int FollowUpsDueToday { get; set; }
    }

    public sealed class CustomerHubDto
    {
        public CustomerHubSummary Summary { get; set; } = new();
        public List<CustomerHubRow> Rows { get; set; } = new();
        public DateTime AsOf { get; set; }
    }

    // ---------------- MAIN ----------------
    public async Task<CustomerHubDto> GetAsync(int companyId, DateTime? asOf = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var today = (asOf ?? DateTime.Today).Date;
        var endOfToday = today.AddDays(1).AddTicks(-1);

        // 1) Customers
        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .Select(c => new
            {
                c.CustomerId,
                c.Name,
                c.CustomerType,
                c.TRN,
                c.City,
                c.NextFollowUpDate,
                c.NextFollowUpNote
            })
            .ToListAsync();

        // 2) Invoices (typed)
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId)
            .Select(i => new InvoiceLite
            {
                InvoiceId = i.InvoiceId,
                CustomerId = i.CustomerId,
                InvoiceDate = i.InvoiceDate,
                DueDate = (i.DueDate ?? i.InvoiceDate),
                GrandTotal = i.GrandTotal
            })
            .ToListAsync();

        // 3) Receipts from Ledger (VoucherType="REC", RefId=InvoiceId)
        var receipts = await db.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.CompanyId == companyId
                     && e.VoucherType == "REC"
                     && e.RefId != null
                     && e.TxnDate <= endOfToday)
            .Select(e => new ReceiptLite
            {
                InvoiceId = e.RefId!.Value,
                TxnDate = e.TxnDate,
                Amount = e.Amount
            })
            .ToListAsync();

        // Lookups
        var paidByInvoice = receipts
            .GroupBy(r => r.InvoiceId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var lastPayDateByInvoice = receipts
            .GroupBy(r => r.InvoiceId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.TxnDate));

        var invByCustomer = invoices
            .GroupBy(i => i.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        DateTime? GetCustomerLastPaymentDate(int customerId)
        {
            if (!invByCustomer.TryGetValue(customerId, out var invs) || invs == null || invs.Count == 0)
                return null;

            DateTime? max = null;
            foreach (var inv in invs)
            {
                if (lastPayDateByInvoice.TryGetValue(inv.InvoiceId, out var d))
                {
                    if (max == null || d > max) max = d;
                }
            }
            return max;
        }

        var rows = new List<CustomerHubRow>();

        foreach (var c in customers)
        {
            invByCustomer.TryGetValue(c.CustomerId, out List<InvoiceLite>? invs);
            invs ??= new List<InvoiceLite>(); // ✅ fixed

            decimal outstanding = 0m;
            int overdueCount = 0;
            int maxOverdueDays = 0;
            DateTime? lastInvoiceDate = null;

            foreach (var inv in invs)
            {
                var paid = paidByInvoice.TryGetValue(inv.InvoiceId, out var p) ? p : 0m;
                var bal = inv.GrandTotal - paid;
                if (bal < 0) bal = 0;

                outstanding += bal;

                if (lastInvoiceDate == null || inv.InvoiceDate > lastInvoiceDate)
                    lastInvoiceDate = inv.InvoiceDate;

                if (bal > 0 && inv.DueDate.Date < today)
                {
                    overdueCount++;
                    var days = (today - inv.DueDate.Date).Days;
                    if (days > maxOverdueDays) maxOverdueDays = days;
                }
            }

            var risk =
                overdueCount == 0 ? "Green"
                : maxOverdueDays <= 30 ? "Amber"
                : "Red";

            rows.Add(new CustomerHubRow
            {
                CustomerId = c.CustomerId,
                Name = c.Name,
                CustomerType = c.CustomerType,
                TRN = c.TRN,
                City = c.City,

                Outstanding = decimal.Round(outstanding, 2),
                OverdueCount = overdueCount,
                LastInvoiceDate = lastInvoiceDate,
                LastPaymentDate = GetCustomerLastPaymentDate(c.CustomerId),

                NextFollowUpDate = c.NextFollowUpDate,
                NextFollowUpNote = c.NextFollowUpNote,

                MaxOverdueDays = maxOverdueDays,
                Risk = risk
            });
        }

        var summary = new CustomerHubSummary
        {
            TotalCustomers = rows.Count,
            TotalOutstanding = rows.Sum(x => x.Outstanding),
            TotalOverdueInvoices = rows.Sum(x => x.OverdueCount),
            FollowUpsDueToday = rows.Count(x => x.NextFollowUpDate.HasValue && x.NextFollowUpDate.Value.Date == today)
        };

        rows = rows
            .OrderByDescending(x => x.Risk == "Red")
            .ThenByDescending(x => x.OverdueCount)
            .ThenByDescending(x => x.Outstanding)
            .ThenBy(x => x.Name)
            .ToList();

        return new CustomerHubDto { Summary = summary, Rows = rows, AsOf = today };
    }

    public async Task SetFollowUpAsync(int companyId, int customerId, DateTime? followUpDate, string? note)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var c = await db.Customers.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.CustomerId == customerId);
        if (c == null) throw new Exception("Customer not found.");

        c.NextFollowUpDate = followUpDate?.Date;
        c.NextFollowUpNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        await db.SaveChangesAsync();
    }
}
