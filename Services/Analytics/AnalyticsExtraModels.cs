namespace UaeEInvoice.Services.Analytics;

public record TopCustomerRow(
    int CustomerId,
    string CustomerName,
    decimal SalesTotal,
    int InvoiceCount
);
