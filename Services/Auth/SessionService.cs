using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;
using UaeEInvoice.Data.Models;

namespace UaeEInvoice.Services.Auth;

public class SessionService
{
    private readonly AppDbContext _db;

    public SessionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UserSession> CreateSessionAsync(string userId, int companyId, string userName, int days = 7)
    {
        // old sessions optional: deactivate
        // var old = await _db.UserSessions.Where(x => x.UserId == userId && x.IsActive).ToListAsync();
        // foreach (var s in old) s.IsActive = false;

        var guid = Guid.NewGuid().ToString();

        var session = new UserSession
        {
            SessionGuid = guid,
            UserId = userId,
            CompanyId = companyId,
            UserName = userName ?? "",
            CreatedOnUtc = DateTime.UtcNow,
            ExpiresOnUtc = DateTime.UtcNow.AddDays(days),
            IsActive = true
        };

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();

        return session;
    }

    public async Task<UserSession?> ValidateSessionAsync(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return null;

        return await _db.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.SessionGuid == guid &&
                x.IsActive &&
                x.ExpiresOnUtc > DateTime.UtcNow);
    }

    public async Task<bool> LogoutAsync(string guid)
    {
        var s = await _db.UserSessions.FirstOrDefaultAsync(x => x.SessionGuid == guid && x.IsActive);
        if (s == null) return false;

        s.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }
}
