using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Auth
{
    public class CompanyFeatureService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;

        public CompanyFeatureService(IDbContextFactory<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<HashSet<string>> GetEnabledKeysAsync(int companyId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            await EnsureDefaultsAsync(db, companyId);

            var keys = await db.CompanyFeatures.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.IsEnabled)
                .Select(x => x.FeatureKey)
                .ToListAsync();

            return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        }

        public static async Task EnsureDefaultsAsync(AppDbContext db, int companyId)
        {
            string[] defaults =
            {
                FeatureKeys.HOME,
                FeatureKeys.COMPANY_SETUP,
                FeatureKeys.MASTERS,
                FeatureKeys.SALES,
                FeatureKeys.PURCHASE,
                FeatureKeys.ACCOUNTING,
                FeatureKeys.AI,
                FeatureKeys.CHEQUES,
                FeatureKeys.ADMIN,
                FeatureKeys.REPORTS,
                FeatureKeys.CRM,
                FeatureKeys.BI
            };

            var existing = await db.CompanyFeatures.AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .Select(x => x.FeatureKey)
                .ToListAsync();

            var add = new List<CompanyFeature>();

            foreach (var k in defaults)
            {
                if (!existing.Contains(k, StringComparer.OrdinalIgnoreCase))
                    add.Add(new CompanyFeature { CompanyId = companyId, FeatureKey = k, IsEnabled = true });
            }

            if (add.Count > 0)
            {
                db.CompanyFeatures.AddRange(add);
                await db.SaveChangesAsync();
            }
        }
    }
}
