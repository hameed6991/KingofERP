using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class CustomerNote
    {
        public int CustomerNoteId { get; set; }
        public int CompanyId { get; set; }
        public int CustomerId { get; set; }

        [MaxLength(30)]
        public string NoteType { get; set; } = "Call";  // Call/WhatsApp/Meeting/Other

        [Required, MaxLength(2000)]
        public string NoteText { get; set; } = "";

        public bool IsImportant { get; set; } = false;

        [MaxLength(80)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
