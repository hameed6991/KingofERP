using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.CRM;

public class CustomerTasksService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public CustomerTasksService(IDbContextFactory<AppDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public enum TaskBucket { DueToday, Overdue, Upcoming, NoDate }

    public sealed class TaskRowDto
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = "";
        public string CustomerType { get; set; } = "";
        public string City { get; set; } = "";
        public string? TRN { get; set; }

        public DateTime? NextFollowUpDate { get; set; }
        public string? NextFollowUpNote { get; set; }

        public decimal BilledTotal { get; set; }
        public DateTime? LastInvoiceDate { get; set; }

        public DateTime? LastNoteDate { get; set; }   // optional if you have CustomerNotes table
        public TaskBucket Bucket { get; set; }
    }

    public sealed class TasksVm
    {
        public int CompanyId { get; set; }
        public DateTime Today { get; set; }
        public int DueTodayCount { get; set; }
        public int OverdueCount { get; set; }
        public int UpcomingCount { get; set; }
        public List<TaskRowDto> Rows { get; set; } = new();
    }

    public async Task<TasksVm> GetAsync(int companyId, DateTime? asOf = null)
    {
        var today = (asOf ?? DateTime.Today).Date;

        await using var db = await _dbFactory.CreateDbContextAsync();

        // 1) Customers (materialize first to avoid DataReader issue)
        var customers = await db.Customers.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new
            {
                x.CustomerId,
                x.Name,
                x.CustomerType,
                x.City,
                x.TRN,
                x.NextFollowUpDate,
                x.NextFollowUpNote
            })
            .ToListAsync();

        var customerIds = customers.Select(x => x.CustomerId).ToList();

        // 2) Invoice aggregates per customer
        var invAgg = await db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && customerIds.Contains(x.CustomerId))
            .GroupBy(x => x.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                BilledTotal = g.Sum(a => a.GrandTotal),
                LastInvoiceDate = g.Max(a => a.InvoiceDate)
            })
            .ToListAsync();

        var invMap = invAgg.ToDictionary(x => x.CustomerId, x => x);

        // 3) Last note date (ONLY if you have DbSet<CustomerNote> CustomerNotes)
        // If you don't have it yet, comment this block + LastNoteDate usage.
        Dictionary<int, DateTime?> noteMap = new();
        try
        {
            var noteAgg = await db.CustomerNotes.AsNoTracking()
                .Where(n => n.CompanyId == companyId && customerIds.Contains(n.CustomerId))
                .GroupBy(n => n.CustomerId)
                .Select(g => new { CustomerId = g.Key, LastNoteDate = g.Max(x => x.CreatedAt) })
                .ToListAsync();

            noteMap = noteAgg.ToDictionary(x => x.CustomerId, x => (DateTime?)x.LastNoteDate);
        }
        catch
        {
            // CustomerNotes not present - safely ignore
        }

        var rows = new List<TaskRowDto>();

        foreach (var c in customers)
        {
            var due = c.NextFollowUpDate?.Date;

            TaskBucket bucket =
                due == null ? TaskBucket.NoDate :
                due.Value == today ? TaskBucket.DueToday :
                due.Value < today ? TaskBucket.Overdue :
                TaskBucket.Upcoming;

            invMap.TryGetValue(c.CustomerId, out var inv);
            noteMap.TryGetValue(c.CustomerId, out var lastNote);

            // show only customers that have follow-up date set (task)
            if (bucket == TaskBucket.NoDate) continue;

            rows.Add(new TaskRowDto
            {
                CustomerId = c.CustomerId,
                Name = c.Name ?? "",
                CustomerType = c.CustomerType ?? "",
                City = c.City ?? "",
                TRN = c.TRN,

                NextFollowUpDate = c.NextFollowUpDate,
                NextFollowUpNote = c.NextFollowUpNote,

                BilledTotal = inv?.BilledTotal ?? 0m,
                LastInvoiceDate = inv?.LastInvoiceDate,

                LastNoteDate = lastNote,
                Bucket = bucket
            });
        }

        // order: overdue first, then today, then upcoming
        rows = rows
            .OrderBy(r => r.Bucket == TaskBucket.Overdue ? 0 : r.Bucket == TaskBucket.DueToday ? 1 : 2)
            .ThenBy(r => r.NextFollowUpDate)
            .ThenBy(r => r.Name)
            .ToList();

        return new TasksVm
        {
            CompanyId = companyId,
            Today = today,
            DueTodayCount = rows.Count(x => x.Bucket == TaskBucket.DueToday),
            OverdueCount = rows.Count(x => x.Bucket == TaskBucket.Overdue),
            UpcomingCount = rows.Count(x => x.Bucket == TaskBucket.Upcoming),
            Rows = rows
        };
    }

    public async Task SaveCustomerFollowUpAsync(int companyId, int customerId, DateTime? followUpDate, string? note)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var c = await db.Customers.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.CustomerId == customerId);
        if (c == null) throw new Exception("Customer not found.");

        c.NextFollowUpDate = followUpDate?.Date;
        c.NextFollowUpNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        await db.SaveChangesAsync();
    }

    public async Task<List<(int Id, string Name, string Type, string City, string? TRN)>> GetCustomersLookupAsync(int companyId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var rows = await db.Customers.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .Select(x => new { x.CustomerId, x.Name, x.CustomerType, x.City, x.TRN })
            .ToListAsync();

        return rows.Select(x => (x.CustomerId, x.Name ?? "", x.CustomerType ?? "", x.City ?? "", x.TRN)).ToList();
    }
}
