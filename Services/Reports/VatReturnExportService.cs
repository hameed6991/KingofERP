using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UaeEInvoice.Services.Reports;

namespace UaeEInvoice.Services.Reports;

public class VatReturnExportService
{
    public byte[] BuildPdf(VatReportService.VatReturnDto vm, string companyName)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(25);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text($"{companyName}").SemiBold().FontSize(16);
                    col.Item().Text("VAT Return Summary (UAE)").FontSize(12).SemiBold();
                    col.Item().Text($"Period: {vm.FromDate:dd-MMM-yyyy} to {vm.ToDate:dd-MMM-yyyy}")
                        .FontColor(Colors.Grey.Darken1);
                    col.Item().LineHorizontal(1);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Text("Summary").SemiBold().FontSize(12);

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.ConstantColumn(120);
                            c.ConstantColumn(120);
                            c.ConstantColumn(120);
                        });

                        void Row(string label, decimal taxable, decimal vat, decimal gross)
                        {
                            t.Cell().Element(CellHead).Text(label).SemiBold();
                            t.Cell().Element(Cell).AlignRight().Text($"{taxable:0.00}");
                            t.Cell().Element(Cell).AlignRight().Text($"{vat:0.00}");
                            t.Cell().Element(Cell).AlignRight().Text($"{gross:0.00}");
                        }

                        t.Header(h =>
                        {
                            h.Cell().Element(CellHead).Text("Type");
                            h.Cell().Element(CellHead).AlignRight().Text("Taxable");
                            h.Cell().Element(CellHead).AlignRight().Text("VAT");
                            h.Cell().Element(CellHead).AlignRight().Text("Gross");
                        });

                        Row("Sales (Output VAT)", vm.Sales.Taxable, vm.Sales.Vat, vm.Sales.Gross);
                        Row("Purchases (Input VAT)", vm.Purchases.Taxable, vm.Purchases.Vat, vm.Purchases.Gross);

                        t.Cell().ColumnSpan(4).PaddingTop(6).LineHorizontal(1);

                        t.Cell().Element(CellHead).Text("Net VAT Payable / (Refund)").SemiBold();
                        t.Cell().ColumnSpan(3).Element(Cell).AlignRight().Text($"{vm.NetVatPayable:0.00}").SemiBold();
                    });

                    col.Item().Text("Sales Invoices").SemiBold().FontSize(12);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(80);
                            c.ConstantColumn(90);
                            c.RelativeColumn();
                            c.ConstantColumn(80);
                            c.ConstantColumn(70);
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(CellHead).Text("Date");
                            h.Cell().Element(CellHead).Text("Invoice");
                            h.Cell().Element(CellHead).Text("Customer");
                            h.Cell().Element(CellHead).AlignRight().Text("VAT");
                            h.Cell().Element(CellHead).AlignRight().Text("Gross");
                        });

                        foreach (var d in vm.Sales.Docs.OrderBy(x => x.DocDate))
                        {
                            t.Cell().Element(Cell).Text(d.DocDate.ToString("dd-MMM-yy"));
                            t.Cell().Element(Cell).Text(d.DocNo);
                            t.Cell().Element(Cell).Text(d.PartyName);
                            t.Cell().Element(Cell).AlignRight().Text($"{d.Vat:0.00}");
                            t.Cell().Element(Cell).AlignRight().Text($"{d.Gross:0.00}");
                        }

                        if (!vm.Sales.Docs.Any())
                        {
                            t.Cell().ColumnSpan(5).Element(Cell).Text("No sales invoices.");
                        }
                    });

                    col.Item().Text("Purchase Invoices").SemiBold().FontSize(12);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(80);
                            c.ConstantColumn(90);
                            c.RelativeColumn();
                            c.ConstantColumn(80);
                            c.ConstantColumn(70);
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(CellHead).Text("Date");
                            h.Cell().Element(CellHead).Text("Purchase");
                            h.Cell().Element(CellHead).Text("Vendor");
                            h.Cell().Element(CellHead).AlignRight().Text("VAT");
                            h.Cell().Element(CellHead).AlignRight().Text("Gross");
                        });

                        foreach (var d in vm.Purchases.Docs.OrderBy(x => x.DocDate))
                        {
                            t.Cell().Element(Cell).Text(d.DocDate.ToString("dd-MMM-yy"));
                            t.Cell().Element(Cell).Text(d.DocNo);
                            t.Cell().Element(Cell).Text(d.PartyName);
                            t.Cell().Element(Cell).AlignRight().Text($"{d.Vat:0.00}");
                            t.Cell().Element(Cell).AlignRight().Text($"{d.Gross:0.00}");
                        }

                        if (!vm.Purchases.Docs.Any())
                        {
                            t.Cell().ColumnSpan(5).Element(Cell).Text("No purchase invoices.");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated on ");
                    x.Span(DateTime.Now.ToString("dd-MMM-yyyy HH:mm")).SemiBold();
                });
            });
        });

        return doc.GeneratePdf();

        static IContainer Cell(IContainer c) =>
            c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6);

        static IContainer CellHead(IContainer c) =>
            c.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6);
    }

    // ✅ Excel Export without extra packages (CSV opens in Excel)
    public byte[] BuildExcelCsv(VatReportService.VatReturnDto vm, string companyName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{companyName}");
        sb.AppendLine("VAT Return Summary (UAE)");
        sb.AppendLine($"Period,{vm.FromDate:dd-MMM-yyyy},{vm.ToDate:dd-MMM-yyyy}");
        sb.AppendLine();

        sb.AppendLine("Section,Taxable,VAT,Gross");
        sb.AppendLine($"Sales (Output VAT),{vm.Sales.Taxable:0.00},{vm.Sales.Vat:0.00},{vm.Sales.Gross:0.00}");
        sb.AppendLine($"Purchases (Input VAT),{vm.Purchases.Taxable:0.00},{vm.Purchases.Vat:0.00},{vm.Purchases.Gross:0.00}");
        sb.AppendLine($"Net VAT Payable/Refund,,{vm.NetVatPayable:0.00},");
        sb.AppendLine();

        sb.AppendLine("Sales Invoices");
        sb.AppendLine("Date,Invoice,Customer,TRN,Taxable,VAT,Gross");
        foreach (var d in vm.Sales.Docs.OrderBy(x => x.DocDate))
            sb.AppendLine($"{d.DocDate:dd-MMM-yyyy},{Escape(d.DocNo)},{Escape(d.PartyName)},{Escape(d.PartyTRN)},{d.Taxable:0.00},{d.Vat:0.00},{d.Gross:0.00}");
        sb.AppendLine();

        sb.AppendLine("Purchase Invoices");
        sb.AppendLine("Date,Purchase,Vendor,TRN,Taxable,VAT,Gross");
        foreach (var d in vm.Purchases.Docs.OrderBy(x => x.DocDate))
            sb.AppendLine($"{d.DocDate:dd-MMM-yyyy},{Escape(d.DocNo)},{Escape(d.PartyName)},{Escape(d.PartyTRN)},{d.Taxable:0.00},{d.Vat:0.00},{d.Gross:0.00}");

        return Encoding.UTF8.GetBytes(sb.ToString());

        static string Escape(string? s)
        {
            s ??= "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
