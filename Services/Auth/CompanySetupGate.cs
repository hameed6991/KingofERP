using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Auth;

public class CompanySetupGate : ICompanySetupGate
{
    private readonly IDbContextFactory<AppDbContext> _dbf;
    private readonly ICurrentCompany _current;

    private CompanySetupStatus? _cache;
    private DateTime _cacheAtUtc;

    // cache window to reduce DB hit
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(10);

    public CompanySetupGate(IDbContextFactory<AppDbContext> dbf, ICurrentCompany current)
    {
        _dbf = dbf;
        _current = current;
    }

    public async Task<CompanySetupStatus> GetStatusAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cache != null && (DateTime.UtcNow - _cacheAtUtc) < CacheFor)
            return _cache;

        await _current.RefreshAsync();

        var isAuth = _current.IsAuthenticated;
        var cid = _current.CompanyId;
        var hasClaim = cid > 0;

        bool setupComplete = false;

        if (isAuth && hasClaim)
        {
            await using var db = await _dbf.CreateDbContextAsync();

            // ✅ Setup completed condition:
            // Company record exists = setup done
            setupComplete = await db.Companies
                .AsNoTracking()
                .AnyAsync(x => x.CompanyId == cid);
        }

        _cache = new CompanySetupStatus(isAuth, cid, hasClaim, setupComplete);
        _cacheAtUtc = DateTime.UtcNow;

        return _cache;
    }

    public async Task<bool> IsSetupCompleteAsync(bool forceRefresh = false)
        => (await GetStatusAsync(forceRefresh)).IsSetupComplete;
}
