using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.CRM
{
    public class CustomerNotesService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;

        public CustomerNotesService(IDbContextFactory<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<List<CustomerNote>> ListAsync(int companyId, int customerId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.CustomerNotes.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.CustomerId == customerId)
                .OrderByDescending(x => x.IsImportant)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(int companyId, int customerId, string type, string text, bool important, string? createdBy)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new Exception("Note text is required.");

            type = string.IsNullOrWhiteSpace(type) ? "Other" : type.Trim();
            text = text.Trim();

            await using var db = await _dbFactory.CreateDbContextAsync();

            db.CustomerNotes.Add(new CustomerNote
            {
                CompanyId = companyId,
                CustomerId = customerId,
                NoteType = type,
                NoteText = text,
                IsImportant = important,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int companyId, int noteId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var n = await db.CustomerNotes.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.CustomerNoteId == noteId);
            if (n == null) return;

            db.CustomerNotes.Remove(n);
            await db.SaveChangesAsync();
        }
    }
}
