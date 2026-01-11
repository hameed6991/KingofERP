using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public sealed class InventoryPostingService
{
    private readonly AppDbContext _db;
    public InventoryPostingService(AppDbContext db) => _db = db;

    public const string TxnPurchase = "PURCHASE";
    public const string TxnSale = "SALE";

    public sealed record StockLine(int ItemId, string ItemName, decimal Qty, decimal UnitCost);

    /// <summary>
    /// Posts stock ledger. QtyChange: + for Purchase, - for Sale.
    /// Skips Service items automatically.
    /// </summary>
    public async Task PostAsync(
        int companyId,
        DateTime txnDate,
        string txnType,
        string refNo,
        string? notes,
        IEnumerable<StockLine> lines,
        bool validateStockForOut = true)
    {
        txnDate = txnDate.Date;

        var list = lines
            .Where(x => x.ItemId > 0 && x.Qty > 0)
            .ToList();

        if (list.Count == 0) return;

        var ids = list.Select(x => x.ItemId).Distinct().ToList();

        // ✅ Skip Service items
        var typeMap = await _db.Items.AsNoTracking()
            .Where(i => i.CompanyId == companyId && ids.Contains(i.ItemId))
            .Select(i => new { i.ItemId, i.ItemType })
            .ToDictionaryAsync(x => x.ItemId, x => (x.ItemType ?? "Product").Trim().ToUpperInvariant());

        list = list.Where(x =>
        {
            typeMap.TryGetValue(x.ItemId, out var t);
            return (t ?? "PRODUCT") != "SERVICE";
        }).ToList();

        if (list.Count == 0) return;

        // ✅ Validate stock if SALE
        if (validateStockForOut && txnType == TxnSale)
        {
            var availableMap = await _db.StockLedgers.AsNoTracking()
                .Where(s => s.CompanyId == companyId && ids.Contains(s.ItemId))
                .GroupBy(s => s.ItemId)
                .Select(g => new { ItemId = g.Key, Qty = g.Sum(x => x.QtyChange) })
                .ToDictionaryAsync(x => x.ItemId, x => x.Qty);

            foreach (var ln in list)
            {
                availableMap.TryGetValue(ln.ItemId, out var available);
                if (available < ln.Qty)
                    throw new Exception($"Insufficient stock for {ln.ItemName}. Available: {available:0.##}, Requested: {ln.Qty:0.##}");
            }
        }

        foreach (var ln in list)
        {
            var qtyChange = txnType == TxnSale ? -ln.Qty : ln.Qty;

            _db.StockLedgers.Add(new StockLedger
            {
                CompanyId = companyId,
                TxnDate = txnDate,
                ItemId = ln.ItemId,
                ItemName = ln.ItemName ?? "",
                QtyChange = qtyChange,
                UnitCost = ln.UnitCost,
                TxnType = txnType,  // PURCHASE / SALE
                RefNo = refNo,
                Notes = notes
            });
        }

        await _db.SaveChangesAsync();
    }
}
