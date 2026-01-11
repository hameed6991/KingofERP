namespace UaeEInvoice.Controllers.Dtos;

public class CreateUserDto
{
    public int CompanyId { get; set; } = 1;
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "Sales"; // Admin/Sales/Purchase/Inventory
    public bool EmailConfirmed { get; set; } = true;
}
