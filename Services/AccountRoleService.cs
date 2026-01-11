using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public sealed class AccountRoleService
{
    private readonly AppDbContext _db;

    public AccountRoleService(AppDbContext db)
    {
        _db = db;
    }

    // -----------------------------
    // ✅ NEW: Helpers (no hardcoding)
    // -----------------------------

    private static string NormKey(string? key)
        => (key ?? "").Trim().ToUpperInvariant();

    public async Task<List<int>> GetAccountNosAsync(int companyId, string roleKey, bool activeOnly = true)
    {
        roleKey = NormKey(roleKey);

        var q = _db.Set<AccountRoleMap>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.RoleKey == roleKey);

        if (activeOnly)
            q = q.Where(x => x.IsActive);

        return await q
            .OrderBy(x => x.AccountNo)
            .Select(x => x.AccountNo)
            .Distinct()
            .ToListAsync();
    }

    // Most pages need single account number (eg: Cash ledger card / AP ledger card)
    public async Task<int?> GetPrimaryAccountNoAsync(int companyId, string roleKey, bool activeOnly = true)
    {
        var list = await GetAccountNosAsync(companyId, roleKey, activeOnly);
        return list.Count > 0 ? list[0] : null;
    }

    // If mapping must exist, use this (throws clean error)
    public async Task<int> RequirePrimaryAccountNoAsync(int companyId, string roleKey, string friendlyName)
    {
        var no = await GetPrimaryAccountNoAsync(companyId, roleKey, activeOnly: true);
        if (no == null || no.Value <= 0)
            throw new InvalidOperationException($"{friendlyName} account is not mapped. Go to Account Roles and set {roleKey}.");
        return no.Value;
    }

    // Payment posting helper: Cash/Bank based on method
    public async Task<int> ResolveCashOrBankAccountAsync(int companyId, string? method)
    {
        method = (method ?? "").Trim();

        // You can adjust rules here
        var isBank =
            method.Equals("Bank", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("Card", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("Online", StringComparison.OrdinalIgnoreCase);

        if (isBank)
            return await RequirePrimaryAccountNoAsync(companyId, AccountRoleKeys.BANK, "Bank");

        return await RequirePrimaryAccountNoAsync(companyId, AccountRoleKeys.CASH, "Cash");
    }

    // -----------------------------
    // Existing: Full role sets for forecast engine
    // -----------------------------
    public async Task<AccountRolesDto> GetRolesAsync(int companyId)
    {
        var rows = await _db.Set<AccountRoleMap>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive)
            .ToListAsync();

        var dto = new AccountRolesDto { CompanyId = companyId };

        foreach (var r in rows)
        {
            var key = NormKey(r.RoleKey);

            if (key == AccountRoleKeys.CASH) dto.CashAccounts.Add(r.AccountNo);
            else if (key == AccountRoleKeys.BANK) dto.BankAccounts.Add(r.AccountNo);
            else if (key == AccountRoleKeys.AR) dto.ArAccounts.Add(r.AccountNo);
            else if (key == AccountRoleKeys.AP) dto.ApAccounts.Add(r.AccountNo);
        }

        dto.CashAccounts = dto.CashAccounts.Distinct().ToList();
        dto.BankAccounts = dto.BankAccounts.Distinct().ToList();
        dto.ArAccounts = dto.ArAccounts.Distinct().ToList();
        dto.ApAccounts = dto.ApAccounts.Distinct().ToList();

        return dto;
    }

    // Existing: Rows for UI grid
    public async Task<List<AccountRoleRowDto>> GetRoleRowsAsync(int companyId)
    {
        var maps = await _db.Set<AccountRoleMap>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.RoleKey)
            .ThenBy(x => x.AccountNo)
            .ToListAsync();

        var accNos = maps.Select(x => x.AccountNo).Distinct().ToList();

        // if no mappings, avoid "IN ()" issues
        if (accNos.Count == 0)
            return new List<AccountRoleRowDto>();

        var coa = await _db.Set<ChartOfAccount>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && accNos.Contains(x.AccountNo))
            .ToListAsync();

        var nameByNo = coa.ToDictionary(x => x.AccountNo, x => x.AccountName);

        return maps.Select(m => new AccountRoleRowDto
        {
            Id = m.Id,
            CompanyId = m.CompanyId,
            RoleKey = m.RoleKey,
            AccountNo = m.AccountNo,
            AccountName = nameByNo.TryGetValue(m.AccountNo, out var n) ? n : "",
            IsActive = m.IsActive
        }).ToList();
    }

    // Existing: Add / Reactivate mapping (supports multiple)
    public async Task UpsertAsync(int companyId, string roleKey, int accountNo)
    {
        roleKey = NormKey(roleKey);

        var coaOk = await _db.Set<ChartOfAccount>()
            .AsNoTracking()
            .AnyAsync(x => x.CompanyId == companyId && x.AccountNo == accountNo);

        if (!coaOk)
            throw new InvalidOperationException($"AccountNo {accountNo} not found in COA for CompanyId={companyId}.");

        var existing = await _db.Set<AccountRoleMap>()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.RoleKey == roleKey && x.AccountNo == accountNo);

        if (existing == null)
        {
            existing = new AccountRoleMap
            {
                CompanyId = companyId,
                RoleKey = roleKey,
                AccountNo = accountNo,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Add(existing);
        }
        else
        {
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task SetActiveAsync(int id, bool active)
    {
        var row = await _db.Set<AccountRoleMap>().FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return;

        row.IsActive = active;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var row = await _db.Set<AccountRoleMap>().FirstOrDefaultAsync(x => x.Id == id);
        if (row == null) return;

        _db.Remove(row);
        await _db.SaveChangesAsync();
    }
}
