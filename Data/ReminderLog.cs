using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data;

public class ReminderLog
{
    public int ReminderLogId { get; set; }

    public int CompanyId { get; set; }

    [MaxLength(10)]
    public string DocType { get; set; } = "INV"; // INV / PINV

    public int DocId { get; set; } // InvoiceId / PurchaseInvoiceId

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    [MaxLength(40)]
    public string Channel { get; set; } = "Manual"; // Manual / Email / WhatsApp later

    [MaxLength(400)]
    public string? Note { get; set; }

    [MaxLength(120)]
    public string? SentTo { get; set; } // phone/email optional
}
