using System.ComponentModel.DataAnnotations;

namespace UaeEInvoice.Data
{
    public class DocSequence
    {
        public int DocSequenceId { get; set; }
        public int CompanyId { get; set; }

        [MaxLength(30)]
        public string DocType { get; set; } = "CPB"; // CPB

        [MaxLength(10)]
        public string Prefix { get; set; } = "CPB";

        public int NextNumber { get; set; } = 1;
        public int Pad { get; set; } = 4;
    }
}
