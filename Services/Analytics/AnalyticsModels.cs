namespace UaeEInvoice.Services.Analytics;

public record ExecKpis(
    decimal SalesMtd,
    decimal PurchasesMtd,
    decimal ArOutstanding,
    decimal ArOverdue,
    decimal ApOutstanding,
    decimal ApOverdue,
    decimal VatOutputMtd,
    decimal VatInputMtd,
    decimal VatNetMtd
);

public record TrendPoint(DateTime Day, decimal Value, int Count);

public record AgingBucket(string Bucket, decimal Amount, int Count);
public record AgingResult(List<AgingBucket> Buckets, decimal Total);
