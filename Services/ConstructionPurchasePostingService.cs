using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public class ConstructionPurchasePostingService
{
    private readonly AppDbContext _db;

    // Optional: if you already have an inventory posting service, inject it.
    // If you don't have it yet, delete this line + calls below.
    private readonly InventoryPostingService? _stock;

    public ConstructionPurchasePostingService(AppDbContext db, InventoryPostingService? stock = null)
    {
        _db = db;
        _stock = stock;
    }

    // TODO: later make these configurable per Company
    private const int AP_VENDOR = 2100;
    private const int VAT_INPUT = 1330;
    private const int CONSTRUCTION_MATERIALS = 5100; // expense bucket
    private const int INVENTORY_ASSET = 1400;        // if ReceiveMode = Store

    public async Task PostAsync(int companyId, int billId)
    {
        var b = await _db.ConstructionPurchaseBills
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ConstructionPurchaseBillId == billId);

        if (b == null) throw new Exception("Construction purchase bill not found.");
        if (b.Status == "Posted") return;

        if (b.ReceiveMode == "DirectToSite" && (b.ProjectId == null || b.ProjectId <= 0))
            throw new Exception("Direct to Site mode requires Project.");

        // Remove old GL (if repost)
        var old = await _db.GeneralLedgerEntries
            .Where(x => x.CompanyId == companyId && x.VoucherType == "CPB" && x.RefId == billId)
            .ToListAsync();
        if (old.Count > 0) _db.GeneralLedgerEntries.RemoveRange(old);

        var nar = $"CP Bill {b.BillNo} - {b.VendorName}";
        if (!string.IsNullOrWhiteSpace(b.ProjectName)) nar += $" | {b.ProjectName}";

        // Choose debit account
        var debitMain = (b.ReceiveMode == "Store") ? INVENTORY_ASSET : CONSTRUCTION_MATERIALS;

        if (b.SubTotal > 0)
        {
            _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                CompanyId = companyId,
                TxnDate = b.BillDate,
                VoucherType = "CPB",
                VoucherNo = b.BillNo,
                RefId = billId,
                DebitAccountNo = debitMain,
                CreditAccountNo = AP_VENDOR,
                Amount = b.SubTotal,
                Narration = nar + " (SubTotal)"
            });
        }

        if (b.VatTotal > 0)
        {
            _db.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                CompanyId = companyId,
                TxnDate = b.BillDate,
                VoucherType = "CPB",
                VoucherNo = b.BillNo,
                RefId = billId,
                DebitAccountNo = VAT_INPUT,
                CreditAccountNo = AP_VENDOR,
                Amount = b.VatTotal,
                Narration = nar + " (VAT Input)"
            });
        }

        // Inventory stock-in only when Store mode
        if (b.ReceiveMode == "Store" && _stock != null)
        {
            // ✅ Example call – adjust to your real InventoryPostingService API
            // await _stock.StockInFromConstructionPurchaseBill(companyId, b.ConstructionPurchaseBillId);
        }

        b.Status = "Posted";
        b.PostedAt = DateTime.UtcNow;
        b.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
