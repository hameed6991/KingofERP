namespace UaeEInvoice.Services;

public sealed class AccountRolesDto
{
    public int CompanyId { get; set; }

    public List<int> CashAccounts { get; set; } = new();
    public List<int> BankAccounts { get; set; } = new();
    public List<int> ArAccounts { get; set; } = new();
    public List<int> ApAccounts { get; set; } = new();

    public HashSet<int> LiquidAccounts =>
        CashAccounts.Concat(BankAccounts).Distinct().ToHashSet();
}

public sealed class AccountRoleRowDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string RoleKey { get; set; } = "";
    public int AccountNo { get; set; }
    public string AccountName { get; set; } = "";
    public bool IsActive { get; set; }
}
