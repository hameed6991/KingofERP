using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Reports;

public class VatReportService
{
    private readonly AppDbContext _db;
    public VatReportService(AppDbContext db) => _db = db;

    // ---------------- DTOs ----------------
    public class VatRateRowDto
    {
        public decimal VatRate { get; set; }
        public decimal Taxable { get; set; }
        public decimal Vat { get; set; }
        public decimal Gross { get; set; }
    }

    public class VatDocRowDto
    {
        public string DocType { get; set; } = ""; // INV / PINV
        public int DocId { get; set; }
        public string DocNo { get; set; } = "";
        public DateTime DocDate { get; set; }
        public string PartyName { get; set; } = "";
        public string? PartyTRN { get; set; }

        public decimal Taxable { get; set; }
        public decimal Vat { get; set; }
        public decimal Gross { get; set; }
    }

    public class VatSideSummaryDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public decimal Taxable { get; set; }
        public decimal Vat { get; set; }
        public decimal Gross { get; set; }

        public List<VatRateRowDto> ByRate { get; set; } = new();
        public List<VatDocRowDto> Docs { get; set; } = new();
    }

    public class VatReturnDto
    {
        public int CompanyId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public VatSideSummaryDto Sales { get; set; } = new();     // Output VAT
        public VatSideSummaryDto Purchases { get; set; } = new(); // Input VAT

        public decimal NetVatPayable { get; set; } // Output - Input
    }

    public class VatFtaBoxesDto
    {
        public int CompanyId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Box 1: Standard rated supplies (5%) – Value + Output VAT
        public decimal Box1_Taxable { get; set; }
        public decimal Box1_OutputVat { get; set; }

        // Box 2: Zero rated supplies – Value
        public decimal Box2_ZeroRated { get; set; }

        // Box 3: Exempt supplies – Value
        public decimal Box3_Exempt { get; set; }

        // Box 9: Standard rated expenses – Value + Input VAT
        public decimal Box9_Taxable { get; set; }
        public decimal Box9_InputVat { get; set; }

        // Net VAT Payable / Refund
        public decimal NetVatPayable => Box1_OutputVat - Box9_InputVat;
    }


    // ---------------- SALES (Output VAT) ----------------
    public async Task<VatSideSummaryDto> GetSalesAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        // ✅ do NOT use variable name "from" inside LINQ query syntax
        var fromDt = fromDate.Date;
        var toDt = toDate.Date.AddDays(1).AddTicks(-1);

        var docs = await _db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.InvoiceDate >= fromDt && x.InvoiceDate <= toDt)
            .OrderByDescending(x => x.InvoiceDate)
            .Select(x => new VatDocRowDto
            {
                DocType = "INV",
                DocId = x.InvoiceId,
                DocNo = x.InvoiceNo,
                DocDate = x.InvoiceDate,
                PartyName = x.CustomerName,
                PartyTRN = x.CustomerTRN,
                Taxable = x.SubTotal,
                Vat = x.VatTotal,
                Gross = x.GrandTotal
            })
            .ToListAsync();

        // ✅ Rate breakup from invoice lines (edit LineSubTotal/LineVat/LineTotal if your names differ)
        var byRate = await (
            from l in _db.InvoiceLines.AsNoTracking()
            join h in _db.Invoices.AsNoTracking()
                on new { l.InvoiceId, l.CompanyId } equals new { InvoiceId = h.InvoiceId, CompanyId = h.CompanyId }
            where l.CompanyId == companyId
                  && h.InvoiceDate >= fromDt
                  && h.InvoiceDate <= toDt
            group l by l.VatRate into g
            select new VatRateRowDto
            {
                VatRate = g.Key,
                Taxable = g.Sum(x => x.LineSubTotal),
                Vat = g.Sum(x => x.LineVat),
                Gross = g.Sum(x => x.LineTotal)
            }
        ).OrderByDescending(x => x.VatRate).ToListAsync();

        return new VatSideSummaryDto
        {
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            Taxable = docs.Sum(x => x.Taxable),
            Vat = docs.Sum(x => x.Vat),
            Gross = docs.Sum(x => x.Gross),
            Docs = docs,
            ByRate = byRate
        };
    }

    // ---------------- PURCHASES (Input VAT) ----------------
    // ✅ Header totals only (works now). Later we can add PurchaseInvoiceLine breakup like sales.
    public async Task<VatSideSummaryDto> GetPurchasesAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        var fromDt = fromDate.Date;
        var toDt = toDate.Date.AddDays(1).AddTicks(-1);

        var docs = await _db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.PurchaseDate >= fromDt && x.PurchaseDate <= toDt)
            .OrderByDescending(x => x.PurchaseDate)
            .Select(x => new VatDocRowDto
            {
                DocType = "PINV",
                DocId = x.PurchaseInvoiceId,
                DocNo = x.PurchaseNo,
                DocDate = x.PurchaseDate,
                PartyName = x.VendorName,
                PartyTRN = x.VendorTRN,
                Taxable = x.SubTotal,
                Vat = x.VatTotal,
                Gross = x.GrandTotal
            })
            .ToListAsync();

        // ✅ Line-level breakup for Purchases (using PurchaseInvoiceLines)
        var byRate = await (
            from l in _db.PurchaseInvoiceLines.AsNoTracking()
            join h in _db.PurchaseInvoices.AsNoTracking()
                on new { l.PurchaseInvoiceId, l.CompanyId } equals new { PurchaseInvoiceId = h.PurchaseInvoiceId, CompanyId = h.CompanyId }
            where l.CompanyId == companyId
                  && h.PurchaseDate >= fromDt
                  && h.PurchaseDate <= toDt
            group l by (l.VatRate > 1m ? l.VatRate / 100m : l.VatRate) into g
            select new VatRateRowDto
            {
                VatRate = g.Key,                  // normalized (0.05)
                Taxable = g.Sum(x => x.LineNet),   // before VAT
                Vat = g.Sum(x => x.LineVat),
                Gross = g.Sum(x => x.LineTotal)
            }
        )
        .OrderByDescending(x => x.VatRate)
        .ToListAsync();

        return new VatSideSummaryDto
        {
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            Taxable = docs.Sum(x => x.Taxable),
            Vat = docs.Sum(x => x.Vat),
            Gross = docs.Sum(x => x.Gross),
            Docs = docs,
            ByRate = byRate
        };
    }
    public async Task<VatFtaBoxesDto> GetFtaBoxesAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        var fromDt = fromDate.Date;
        var toDt = toDate.Date.AddDays(1).AddTicks(-1);

        // ---------------- SALES (Output VAT) boxes ----------------
        var salesGroups = await (
            from l in _db.InvoiceLines.AsNoTracking()
            join h in _db.Invoices.AsNoTracking()
                on new { l.InvoiceId, l.CompanyId } equals new { InvoiceId = h.InvoiceId, CompanyId = h.CompanyId }
            join it in _db.Items.AsNoTracking()
                on new { l.ItemId, l.CompanyId } equals new { ItemId = it.ItemId, CompanyId = it.CompanyId }
            where l.CompanyId == companyId
                  && h.InvoiceDate >= fromDt
                  && h.InvoiceDate <= toDt
            group l by it.VatCategory into g
            select new
            {
                Cat = g.Key,
                Taxable = g.Sum(x => x.LineSubTotal),
                Vat = g.Sum(x => x.LineVat)
            }
        ).ToListAsync();

        decimal box1Taxable = salesGroups.Where(x => x.Cat == Data.VatCategory.Standard).Sum(x => x.Taxable);
        decimal box1Vat = salesGroups.Where(x => x.Cat == Data.VatCategory.Standard).Sum(x => x.Vat);

        decimal box2Zero = salesGroups.Where(x => x.Cat == Data.VatCategory.Zero).Sum(x => x.Taxable);
        decimal box3Exempt = salesGroups.Where(x => x.Cat == Data.VatCategory.Exempt).Sum(x => x.Taxable);

        // ---------------- PURCHASES (Input VAT) box 9 ----------------
        var purchaseGroups = await (
            from l in _db.PurchaseInvoiceLines.AsNoTracking()
            join h in _db.PurchaseInvoices.AsNoTracking()
                on new { l.PurchaseInvoiceId, l.CompanyId } equals new { PurchaseInvoiceId = h.PurchaseInvoiceId, CompanyId = h.CompanyId }
            join it in _db.Items.AsNoTracking()
                on new { l.ItemId, l.CompanyId } equals new { ItemId = it.ItemId, CompanyId = it.CompanyId }
            where l.CompanyId == companyId
                  && h.PurchaseDate >= fromDt
                  && h.PurchaseDate <= toDt
            group l by it.VatCategory into g
            select new
            {
                Cat = g.Key,
                Taxable = g.Sum(x => x.LineNet),
                Vat = g.Sum(x => x.LineVat)
            }
        ).ToListAsync();

        decimal box9Taxable = purchaseGroups.Where(x => x.Cat == Data.VatCategory.Standard).Sum(x => x.Taxable);
        decimal box9Vat = purchaseGroups.Where(x => x.Cat == Data.VatCategory.Standard).Sum(x => x.Vat);

        return new VatFtaBoxesDto
        {
            CompanyId = companyId,
            FromDate = fromDate.Date,
            ToDate = toDate.Date,

            Box1_Taxable = box1Taxable,
            Box1_OutputVat = box1Vat,

            Box2_ZeroRated = box2Zero,
            Box3_Exempt = box3Exempt,

            Box9_Taxable = box9Taxable,
            Box9_InputVat = box9Vat
        };
    }




    // ---------------- VAT RETURN (Sales - Purchases) ----------------
    public async Task<VatReturnDto> GetVatReturnAsync(int companyId, DateTime fromDate, DateTime toDate)
    {
        var sales = await GetSalesAsync(companyId, fromDate, toDate);
        var purchases = await GetPurchasesAsync(companyId, fromDate, toDate);

        return new VatReturnDto
        {
            CompanyId = companyId,
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            Sales = sales,
            Purchases = purchases,
            NetVatPayable = sales.Vat - purchases.Vat
        };
    }
}
