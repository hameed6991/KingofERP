using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Services
{
    public class InvoiceTemplateService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentCompany _currentCompany;

        public InvoiceTemplateService(AppDbContext db, ICurrentCompany currentCompany)
        {
            _db = db;
            _currentCompany = currentCompany;
        }

        private async Task<int> GetCompanyIdAsync()
        {
            await _currentCompany.RefreshAsync();
            if (_currentCompany.CompanyId <= 0)
                throw new InvalidOperationException("Company not found. Logout & login again.");
            return _currentCompany.CompanyId;
        }

        public async Task<List<InvoiceTemplate>> GetLibraryAsync(string? search, string? industry, string? baseKey)
        {
            var cid = await GetCompanyIdAsync();

            industry ??= "All";
            baseKey ??= "All";

            var q = _db.InvoiceTemplates.AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x => x.IsSystem || x.CompanyId == cid);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                q = q.Where(x =>
                    x.Name.Contains(search) ||
                    (x.IndustryTag != null && x.IndustryTag.Contains(search)) ||
                    x.BaseKey.Contains(search));
            }

            if (!string.Equals(industry, "All", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.IndustryTag == industry);

            if (!string.Equals(baseKey, "All", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.BaseKey == baseKey);

            return await q
                .OrderByDescending(x => x.IsSystem)
                .ThenBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<InvoiceTemplate?> GetAsync(int id)
        {
            var cid = await GetCompanyIdAsync();

            var t = await _db.InvoiceTemplates.FirstOrDefaultAsync(x => x.InvoiceTemplateId == id && x.IsActive);
            if (t == null) return null;

            if (!t.IsSystem && t.CompanyId != cid) return null;
            if (t.IsSystem) return t;

            return t;
        }

        public async Task<int> CreateAsync(InvoiceTemplate tpl)
        {
            var cid = await GetCompanyIdAsync();

            tpl.InvoiceTemplateId = 0;
            tpl.CompanyId = cid;
            tpl.IsSystem = false;
            tpl.IsActive = true;
            tpl.CreatedAtUtc = DateTime.UtcNow;
            tpl.UpdatedAtUtc = DateTime.UtcNow;

            // Ensure JSON saved
            tpl.SettingsJson = InvoiceTemplateSettings.FromJson(tpl.SettingsJson).ToJson();

            _db.InvoiceTemplates.Add(tpl);
            await _db.SaveChangesAsync();
            return tpl.InvoiceTemplateId;
        }

        public async Task UpdateAsync(InvoiceTemplate tpl)
        {
            var cid = await GetCompanyIdAsync();

            var row = await _db.InvoiceTemplates.FirstOrDefaultAsync(x => x.InvoiceTemplateId == tpl.InvoiceTemplateId && x.IsActive);
            if (row == null) throw new InvalidOperationException("Template not found.");

            if (row.IsSystem) throw new InvalidOperationException("System template cannot be edited.");
            if (row.CompanyId != cid) throw new InvalidOperationException("Access denied.");

            row.Name = tpl.Name;
            row.IndustryTag = tpl.IndustryTag;
            row.BaseKey = tpl.BaseKey;

            row.SettingsJson = InvoiceTemplateSettings.FromJson(tpl.SettingsJson).ToJson();
            row.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task<int> CloneToCompanyAsync(int templateId)
        {
            var cid = await GetCompanyIdAsync();

            var src = await GetAsync(templateId);
            if (src == null) throw new InvalidOperationException("Template not found.");

            var clone = new InvoiceTemplate
            {
                CompanyId = cid,
                Name = $"{src.Name} (Copy)",
                IndustryTag = src.IndustryTag,
                BaseKey = src.BaseKey,
                IsSystem = false,
                IsActive = true,
                SettingsJson = InvoiceTemplateSettings.FromJson(src.SettingsJson).ToJson(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.InvoiceTemplates.Add(clone);
            await _db.SaveChangesAsync();
            return clone.InvoiceTemplateId;
        }

        public async Task SoftDeleteAsync(int id)
        {
            var cid = await GetCompanyIdAsync();

            var row = await _db.InvoiceTemplates.FirstOrDefaultAsync(x => x.InvoiceTemplateId == id && x.IsActive);
            if (row == null) return;

            if (row.IsSystem) throw new InvalidOperationException("System template cannot be removed.");
            if (row.CompanyId != cid) throw new InvalidOperationException("Access denied.");

            row.IsActive = false;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
