namespace UaeEInvoice.Services.Analytics;

public record ArRecoveryDashboardDto(
    DateTime AsOf,
    int WindowDays,
    decimal SalesInWindow,
    decimal ArOutstanding,
    decimal ArOverdue,
    double DsoDays,
    List<OverdueCustomerRow> TopOverdueCustomers,
    List<FollowUpRow> FollowUps,
    List<TrendPoint> OverdueTrendThisMonth,
    List<TrendPoint> OverdueTrendLastMonth
);

public record OverdueCustomerRow(
    int CustomerId,
    string CustomerName,
    string? Mobile,
    decimal OverdueAmount,
    int InvoiceCount,
    int MaxDaysOverdue
);

public record FollowUpRow(
    int CustomerId,
    string CustomerName,
    string? Mobile,
    DateTime? NextFollowUpDate,
    string? Note
);
