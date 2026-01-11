using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Services.Einvoicing;

public class EinvoiceService
{
    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;

    public EinvoiceService(AppDbContext db, ICurrentCompany currentCompany)
    {
        _db = db;
        _currentCompany = currentCompany;
    }

    /// <summary>
    /// Create or update Draft eInvoice XML for a Sales Invoice.
    /// Returns saved EInvoiceDocument row.
    /// </summary>
    public async Task<EInvoiceDocument> GenerateDraftForInvoiceAsync(int invoiceId)
    {
        await _currentCompany.RefreshAsync();
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) throw new Exception("Company not resolved. Logout & login again.");

        var company = await _db.Companies.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == companyId);

        if (company == null)
            throw new Exception("Company setup not found for current login.");

        var inv = await _db.Invoices.AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.InvoiceId == invoiceId);

        if (inv == null)
            throw new Exception("Invoice not found.");

        var lines = inv.Lines?.OrderBy(x => x.InvoiceLineId).ToList() ?? new List<InvoiceLine>();
        if (lines.Count == 0)
            throw new Exception("Invoice has 0 lines.");

        var profile = "PINT-AE-DRAFT";
        var xml = BuildDraftInvoiceXml(company, inv, lines, profile);
        var hash = Sha256Hex(xml);

        var doc = await _db.EInvoiceDocuments
            .FirstOrDefaultAsync(x =>
                x.CompanyId == companyId &&
                x.SourceType == "INV" &&
                x.SourceId == invoiceId);

        if (doc == null)
        {
            doc = new EInvoiceDocument
            {
                CompanyId = companyId,
                SourceType = "INV",
                SourceId = invoiceId,
                Status = "Draft",
                Profile = profile,
                XmlPayload = xml,
                PayloadHashSha256 = hash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.EInvoiceDocuments.Add(doc);
        }
        else
        {
            if (string.Equals(doc.Status, "Accepted", StringComparison.OrdinalIgnoreCase))
                throw new Exception("eInvoice already Accepted. Do not regenerate.");

            doc.Status = "Draft";
            doc.Profile = profile;
            doc.XmlPayload = xml;
            doc.PayloadHashSha256 = hash;
            doc.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task<string> GetXmlAsync(int eInvoiceDocumentId)
    {
        await _currentCompany.RefreshAsync();
        var companyId = _currentCompany.CompanyId;
        if (companyId <= 0) throw new Exception("Company not resolved.");

        var doc = await _db.EInvoiceDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.EInvoiceDocumentId == eInvoiceDocumentId);

        if (doc == null) throw new Exception("eInvoice document not found.");

        return doc.XmlPayload ?? "";
    }


    // ---------------------------
    // Draft XML Builder
    // ---------------------------
    private static string BuildDraftInvoiceXml(Company company, Invoice inv, List<InvoiceLine> lines, string profile)
    {
        var currency = string.IsNullOrWhiteSpace(company.CurrencyCode) ? "AED" : company.CurrencyCode.Trim().ToUpperInvariant();

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Indent = true,
            Encoding = new UTF8Encoding(false)
        };

        using var sw = new StringWriter(CultureInfo.InvariantCulture);
        using var xw = XmlWriter.Create(sw, settings);

        xw.WriteStartDocument();

        xw.WriteStartElement("EInvoiceDocument");
        xw.WriteAttributeString("profile", profile);
        xw.WriteAttributeString("version", "1.0");

        xw.WriteStartElement("Header");
        xw.WriteElementString("SourceType", "INV");
        xw.WriteElementString("InvoiceId", inv.InvoiceId.ToString(CultureInfo.InvariantCulture));
        xw.WriteElementString("InvoiceNo", inv.InvoiceNo ?? "");
        xw.WriteElementString("IssueDate", inv.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        xw.WriteElementString("DueDate", (inv.DueDate ?? inv.InvoiceDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        xw.WriteElementString("Currency", currency);
        xw.WriteEndElement();

        xw.WriteStartElement("Seller");
        xw.WriteElementString("Name", company.LegalName ?? company.ShortName ?? "Company");
        xw.WriteElementString("TRN", company.TRN ?? "");
        xw.WriteElementString("Emirate", company.Emirate ?? "");
        xw.WriteElementString("City", company.City ?? "");
        xw.WriteElementString("Country", company.Country ?? "United Arab Emirates");
        xw.WriteElementString("AddressLine1", company.AddressLine1 ?? "");
        xw.WriteElementString("AddressLine2", company.AddressLine2 ?? "");
        xw.WriteElementString("POBox", company.POBox ?? "");
        xw.WriteEndElement();

        xw.WriteStartElement("Buyer");
        xw.WriteElementString("Name", inv.CustomerName ?? "");
        xw.WriteElementString("TRN", inv.CustomerTRN ?? "");
        xw.WriteEndElement();

        xw.WriteStartElement("Lines");
        int sl = 1;
        foreach (var ln in lines)
        {
            xw.WriteStartElement("Line");
            xw.WriteElementString("SL", sl.ToString(CultureInfo.InvariantCulture));
            xw.WriteElementString("ItemId", ln.ItemId.ToString(CultureInfo.InvariantCulture));
            xw.WriteElementString("ItemName", ln.ItemName ?? "");
            xw.WriteElementString("Qty", ln.Qty.ToString("0.###", CultureInfo.InvariantCulture));
            xw.WriteElementString("Rate", ln.Rate.ToString("0.00", CultureInfo.InvariantCulture));
            xw.WriteElementString("VatRate", ln.VatRate.ToString("0.00####", CultureInfo.InvariantCulture));
            xw.WriteElementString("LineSubTotal", ln.LineSubTotal.ToString("0.00", CultureInfo.InvariantCulture));
            xw.WriteElementString("LineVat", ln.LineVat.ToString("0.00", CultureInfo.InvariantCulture));
            xw.WriteElementString("LineTotal", ln.LineTotal.ToString("0.00", CultureInfo.InvariantCulture));
            xw.WriteEndElement();
            sl++;
        }
        xw.WriteEndElement();

        xw.WriteStartElement("Totals");
        xw.WriteElementString("SubTotal", inv.SubTotal.ToString("0.00", CultureInfo.InvariantCulture));
        xw.WriteElementString("VatTotal", inv.VatTotal.ToString("0.00", CultureInfo.InvariantCulture));
        xw.WriteElementString("GrandTotal", inv.GrandTotal.ToString("0.00", CultureInfo.InvariantCulture));
        xw.WriteEndElement();

        xw.WriteEndElement();
        xw.WriteEndDocument();
        xw.Flush();

        return sw.ToString();
    }

    private static string Sha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        var hash = SHA256.HashData(bytes);

        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));

        return sb.ToString();
    }
}
