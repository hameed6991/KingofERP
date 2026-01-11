using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public static class InvoicePdfGenerator
{
    public static byte[] Generate(Company company, Invoice inv, List<InvoiceLine> lines)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(col =>
                {
                    // HEADER
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(company.LegalName ?? "Company").SemiBold().FontSize(14);
                            var place = $"{company.City ?? ""} {company.Emirate ?? ""}".Trim();
                            if (!string.IsNullOrWhiteSpace(place))
                                c.Item().Text(place).FontSize(10);

                            c.Item().Text($"TRN: {(string.IsNullOrWhiteSpace(company.TRN) ? "-" : company.TRN)}")
                                .FontSize(10);
                        });

                        r.ConstantItem(220).AlignRight().Column(c =>
                        {
                            c.Item().Text(inv.InvoiceNo).SemiBold().FontSize(14);
                            c.Item().Text($"Date: {inv.InvoiceDate:dd-MM-yyyy}").FontSize(10);
                        });
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1);

                    // BILL TO + SUMMARY
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Bill To").FontSize(10);
                            c.Item().Text(inv.CustomerName).SemiBold();
                            c.Item().Text($"TRN: {(string.IsNullOrWhiteSpace(inv.CustomerTRN) ? "-" : inv.CustomerTRN)}")
                                .FontSize(10);
                        });

                        r.ConstantItem(220).AlignRight().Column(c =>
                        {
                            c.Item().Text("Invoice Summary").FontSize(10);
                            c.Item().Text($"Subtotal: {inv.SubTotal:0.00}").FontSize(10);
                            c.Item().Text($"VAT: {inv.VatTotal:0.00}").FontSize(10);
                            c.Item().Text($"Total: {inv.GrandTotal:0.00}").SemiBold();
                        });
                    });

                    col.Item().PaddingVertical(10);

                    // TABLE
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(6);  // Item
                            columns.RelativeColumn(1);  // Qty
                            columns.RelativeColumn(2);  // Rate
                            columns.RelativeColumn(2);  // VAT
                            columns.RelativeColumn(2);  // Total
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("ITEM");
                            h.Cell().Element(HeaderCell).AlignRight().Text("QTY");
                            h.Cell().Element(HeaderCell).AlignRight().Text("RATE");
                            h.Cell().Element(HeaderCell).AlignRight().Text("VAT");
                            h.Cell().Element(HeaderCell).AlignRight().Text("TOTAL");
                        });

                        foreach (var ln in lines)
                        {
                            t.Cell().Element(BodyCell).Text(ln.ItemName);
                            t.Cell().Element(BodyCell).AlignRight().Text($"{ln.Qty:0.##}");
                            t.Cell().Element(BodyCell).AlignRight().Text($"{ln.Rate:0.00}");
                            t.Cell().Element(BodyCell).AlignRight().Text($"{ln.LineVat:0.00}");
                            t.Cell().Element(BodyCell).AlignRight().Text($"{ln.LineTotal:0.00}");
                        }

                        static IContainer HeaderCell(IContainer c) =>
                            c.PaddingVertical(6).BorderBottom(1).DefaultTextStyle(x => x.SemiBold().FontSize(9));

                        static IContainer BodyCell(IContainer c) =>
                            c.PaddingVertical(6).BorderBottom(0.5f).DefaultTextStyle(x => x.FontSize(10));
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1);

                    // TOTALS
                    col.Item().AlignRight().Width(250).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Subtotal").FontSize(10);
                            r.ConstantItem(100).AlignRight().Text($"{inv.SubTotal:0.00}");
                        });
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("VAT").FontSize(10);
                            r.ConstantItem(100).AlignRight().Text($"{inv.VatTotal:0.00}");
                        });
                        c.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text("Grand Total").SemiBold();
                            r.ConstantItem(100).AlignRight().Text($"{inv.GrandTotal:0.00}").SemiBold();
                        });
                        c.Item().Text("Currency: AED").FontSize(9);
                    });

                    col.Item().PaddingTop(18).AlignCenter()
                        .Text("Thank you for your business.").FontSize(9);
                });
            });
        });

        return doc.GeneratePdf();
    }
}
