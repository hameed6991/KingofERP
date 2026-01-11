using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public class DocSequenceService
{
    private readonly AppDbContext _db;
    public DocSequenceService(AppDbContext db) => _db = db;

    public async Task<(string no, int seq)> NextAsync(int companyId, string docType, string defaultPrefix)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var s = await _db.DocSequences
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.DocType == docType);

        if (s == null)
        {
            s = new DocSequence
            {
                CompanyId = companyId,
                DocType = docType,
                Prefix = defaultPrefix,
                NextNumber = 1,
                Pad = 4
            };
            _db.DocSequences.Add(s);
            await _db.SaveChangesAsync();
        }

        var seq = s.NextNumber;
        s.NextNumber = seq + 1;
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        var no = $"{s.Prefix}-{seq.ToString().PadLeft(s.Pad, '0')}";
        return (no, seq);
    }
}
