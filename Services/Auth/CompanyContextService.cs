using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Services;

public interface ICompanyContextService
{
    Task<CompanyContext> GetAsync(bool forceRefresh = false);
}

public sealed record CompanyContext(
    int CompanyId,
    string Industry,
    bool IsConstruction
);

public class CompanyContextService : ICompanyContextService
{
    private readonly IDbContextFactory<AppDbContext> _dbf;
    private readonly ICurrentCompany _current;

    private CompanyContext? _cache;
    private DateTime _cacheAtUtc;

    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(10);

    public CompanyContextService(IDbContextFactory<AppDbContext> dbf, ICurrentCompany current)
    {
        _dbf = dbf;
        _current = current;
    }

    public async Task<CompanyContext> GetAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cache != null && (DateTime.UtcNow - _cacheAtUtc) < CacheFor)
            return _cache;

        await _current.RefreshAsync();
        var cid = _current.CompanyId;

        string industry = "";
        if (cid > 0)
        {
            await using var db = await _dbf.CreateDbContextAsync();
            industry = await db.Companies
                .AsNoTracking()
                .Where(x => x.CompanyId == cid)
                .Select(x => x.Industry ?? "")
                .FirstOrDefaultAsync() ?? "";
        }

        var isConstruction = industry.Contains("construction", StringComparison.OrdinalIgnoreCase);

        _cache = new CompanyContext(cid, industry, isConstruction);
        _cacheAtUtc = DateTime.UtcNow;

        return _cache;
    }
}
