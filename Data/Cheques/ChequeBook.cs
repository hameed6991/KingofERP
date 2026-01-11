namespace UaeEInvoice.Data;

public class ChequeBook
{
    public int ChequeBookId { get; set; }
    public int CompanyId { get; set; }

    public int BankAccountNo { get; set; }         // GL AccountNo of bank
    public string BankName { get; set; } = "";

    public int StartNo { get; set; }
    public int EndNo { get; set; }
    public int NextNo { get; set; }                // auto increment

    public bool IsActive { get; set; } = true;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
