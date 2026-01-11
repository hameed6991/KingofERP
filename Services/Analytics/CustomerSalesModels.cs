using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Analytics;

public record CustomerInvoiceRow(
    int InvoiceId,
    string InvoiceNo,
    DateTime InvoiceDate,
    DateTime? DueDate,
    decimal Total,
    decimal Paid,
    decimal Balance,
    string Status
);

public record CustomerSalesMtdResult(
    Customer Customer,
    DateTime From,
    DateTime ToExclusive,
    decimal SalesMtd,
    decimal PaidToDateOnMtdInvoices,
    decimal BalanceOnMtdInvoices,
    int InvoiceCount,
    List<CustomerInvoiceRow> Invoices
);
