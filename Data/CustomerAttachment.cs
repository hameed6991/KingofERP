using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class CustomerAttachment
    {
        public int CustomerAttachmentId { get; set; }
        public int CompanyId { get; set; }
        public int CustomerId { get; set; }

        [MaxLength(30)]
        public string FileType { get; set; } = "Other"; // TRN/Contract/Quote/Other

        [Required, MaxLength(260)]
        public string FileName { get; set; } = "";

        [Required, MaxLength(400)]
        public string StoredPath { get; set; } = ""; // ex: uploads/1/customers/9/abc.pdf (under wwwroot)

        public long SizeBytes { get; set; }

        [MaxLength(80)]
        public string? UploadedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
