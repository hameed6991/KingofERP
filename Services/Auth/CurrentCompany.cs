using Microsoft.AspNetCore.Components.Authorization;

namespace UaeEInvoice.Services.Auth;

public interface ICurrentCompany
{
    int CompanyId { get; }
    bool IsAuthenticated { get; }
    Task RefreshAsync();
}

public class CurrentCompany : ICurrentCompany
{
    private readonly AuthenticationStateProvider _auth;

    public CurrentCompany(AuthenticationStateProvider auth)
    {
        _auth = auth;
    }

    public int CompanyId { get; private set; }
    public bool IsAuthenticated { get; private set; }

    public async Task RefreshAsync()
    {
        var state = await _auth.GetAuthenticationStateAsync();
        var user = state.User;

        IsAuthenticated = user.Identity?.IsAuthenticated == true;

        var v = user.FindFirst("CompanyId")?.Value;   // ✅ exact claim name
        CompanyId = int.TryParse(v, out var id) ? id : 0;
    }
}
