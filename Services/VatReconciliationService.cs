using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Reports;

public class VatReconciliationService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    public VatReconciliationService(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    public class DocVatMismatchRow
    {
        public string DocType { get; set; } = ""; // INV / PINV
        public int DocId { get; set; }
        public string DocNo { get; set; } = "";
        public DateTime DocDate { get; set; }
        public string PartyName { get; set; } = "";
        public decimal HeaderVat { get; set; }
        public decimal LinesVat { get; set; }
        public decimal Diff => HeaderVat - LinesVat;
    }

    public class LedgerVatRow
    {
        public DateTime TxnDate { get; set; }
        public string VoucherType { get; set; } = "";
        public string VoucherNo { get; set; } = "";
        public int DebitAccountNo { get; set; }
        public int CreditAccountNo { get; set; }
        public decimal Amount { get; set; }
        public string Effect { get; set; } = "";
    }

    public class VatReconciliationDto
    {
        public int CompanyId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public decimal SalesOutputVat_Docs { get; set; }
        public decimal PurchaseInputVat_Docs { get; set; }
        public decimal NetVatPayable_Docs => SalesOutputVat_Docs - PurchaseInputVat_Docs;

        public int OutputVatAccountNo { get; set; }
        public int InputVatAccountNo { get; set; }

        public decimal OutputVat_Ledger { get; set; }
        public decimal InputVat_Ledger { get; set; }
        public decimal NetVatPayable_Ledger => OutputVat_Ledger - InputVat_Ledger;

        public decimal OutputVat_Diff => SalesOutputVat_Docs - OutputVat_Ledger;
        public decimal InputVat_Diff => PurchaseInputVat_Docs - InputVat_Ledger;
        public decimal NetVat_Diff => NetVatPayable_Docs - NetVatPayable_Ledger;

        public List<DocVatMismatchRow> DocVatMismatches { get; set; } = new();
        public List<LedgerVatRow> LedgerRows { get; set; } = new();
    }

    public async Task<VatReconciliationDto> GetAsync(
        int companyId,
        DateTime fromDate,
        DateTime toDate,
        int outputVatAccountNo,
        int inputVatAccountNo)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var fromDt = fromDate.Date;
        var toDt = toDate.Date.AddDays(1).AddTicks(-1);

        // 1) VAT as per document headers
        var salesVatDocs = await db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.InvoiceDate >= fromDt && x.InvoiceDate <= toDt)
            .SumAsync(x => (decimal?)x.VatTotal) ?? 0m;

        var purchaseVatDocs = await db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.PurchaseDate >= fromDt && x.PurchaseDate <= toDt)
            .SumAsync(x => (decimal?)x.VatTotal) ?? 0m;

        // 2) Invoice header VAT vs line VAT
        var invLineVat = await db.InvoiceLines.AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .GroupBy(l => l.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, LinesVat = g.Sum(x => x.LineVat) })
            .ToListAsync();

        var invHeaders = await db.Invoices.AsNoTracking()
            .Where(h => h.CompanyId == companyId && h.InvoiceDate >= fromDt && h.InvoiceDate <= toDt)
            .Select(h => new { h.InvoiceId, h.InvoiceNo, h.InvoiceDate, h.CustomerName, h.VatTotal })
            .ToListAsync();

        var invMismatch = (
            from h in invHeaders
            join l in invLineVat on h.InvoiceId equals l.InvoiceId into lj
            from l in lj.DefaultIfEmpty()
            let linesVat = l?.LinesVat ?? 0m
            let diff = h.VatTotal - linesVat
            where Math.Abs(diff) > 0.01m
            select new DocVatMismatchRow
            {
                DocType = "INV",
                DocId = h.InvoiceId,
                DocNo = h.InvoiceNo,
                DocDate = h.InvoiceDate,
                PartyName = h.CustomerName,
                HeaderVat = h.VatTotal,
                LinesVat = linesVat
            }
        ).ToList();

        // 3) Purchase header VAT vs line VAT
        var pinvLineVat = await db.PurchaseInvoiceLines.AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .GroupBy(l => l.PurchaseInvoiceId)
            .Select(g => new { PurchaseInvoiceId = g.Key, LinesVat = g.Sum(x => x.LineVat) })
            .ToListAsync();

        var pinvHeaders = await db.PurchaseInvoices.AsNoTracking()
            .Where(h => h.CompanyId == companyId && h.PurchaseDate >= fromDt && h.PurchaseDate <= toDt)
            .Select(h => new { h.PurchaseInvoiceId, h.PurchaseNo, h.PurchaseDate, h.VendorName, h.VatTotal })
            .ToListAsync();

        var pinvMismatch = (
            from h in pinvHeaders
            join l in pinvLineVat on h.PurchaseInvoiceId equals l.PurchaseInvoiceId into lj
            from l in lj.DefaultIfEmpty()
            let linesVat = l?.LinesVat ?? 0m
            let diff = h.VatTotal - linesVat
            where Math.Abs(diff) > 0.01m
            select new DocVatMismatchRow
            {
                DocType = "PINV",
                DocId = h.PurchaseInvoiceId,
                DocNo = h.PurchaseNo,
                DocDate = h.PurchaseDate,
                PartyName = h.VendorName,
                HeaderVat = h.VatTotal,
                LinesVat = linesVat
            }
        ).ToList();

        // 4) Ledger VAT totals
        // NOTE: if your GeneralLedgerEntry uses a different amount field, change x.Amount below
        var ledger = await db.GeneralLedgerEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                     && x.TxnDate >= fromDt
                     && x.TxnDate <= toDt
                     && (x.DebitAccountNo == outputVatAccountNo
                      || x.CreditAccountNo == outputVatAccountNo
                      || x.DebitAccountNo == inputVatAccountNo
                      || x.CreditAccountNo == inputVatAccountNo))
            .OrderBy(x => x.TxnDate)
            .Select(x => new
            {
                x.TxnDate,
                x.VoucherType,
                x.VoucherNo,
                x.DebitAccountNo,
                x.CreditAccountNo,
                Amount = x.Amount
            })
            .ToListAsync();

        decimal outputVatLedger = 0m;
        decimal inputVatLedger = 0m;
        var ledgerRows = new List<LedgerVatRow>();

        foreach (var e in ledger)
        {
            // Output VAT (usually credited)
            if (e.CreditAccountNo == outputVatAccountNo)
            {
                outputVatLedger += e.Amount;
                ledgerRows.Add(MakeRow(e, "OutputVAT + (Credit)"));
            }
            else if (e.DebitAccountNo == outputVatAccountNo)
            {
                outputVatLedger -= e.Amount;
                ledgerRows.Add(MakeRow(e, "OutputVAT - (Debit)"));
            }

            // Input VAT (usually debited)
            if (e.DebitAccountNo == inputVatAccountNo)
            {
                inputVatLedger += e.Amount;
                ledgerRows.Add(MakeRow(e, "InputVAT + (Debit)"));
            }
            else if (e.CreditAccountNo == inputVatAccountNo)
            {
                inputVatLedger -= e.Amount;
                ledgerRows.Add(MakeRow(e, "InputVAT - (Credit)"));
            }
        }

        return new VatReconciliationDto
        {
            CompanyId = companyId,
            FromDate = fromDate.Date,
            ToDate = toDate.Date,

            SalesOutputVat_Docs = salesVatDocs,
            PurchaseInputVat_Docs = purchaseVatDocs,

            OutputVatAccountNo = outputVatAccountNo,
            InputVatAccountNo = inputVatAccountNo,

            OutputVat_Ledger = outputVatLedger,
            InputVat_Ledger = inputVatLedger,

            DocVatMismatches = invMismatch.Concat(pinvMismatch).OrderByDescending(x => x.DocDate).ToList(),
            LedgerRows = ledgerRows.OrderByDescending(x => x.TxnDate).ToList()
        };

        LedgerVatRow MakeRow(dynamic e, string effect) => new()
        {
            TxnDate = e.TxnDate,
            VoucherType = e.VoucherType ?? "",
            VoucherNo = e.VoucherNo ?? "",
            DebitAccountNo = e.DebitAccountNo,
            CreditAccountNo = e.CreditAccountNo,
            Amount = e.Amount,
            Effect = effect
        };
    }
}
