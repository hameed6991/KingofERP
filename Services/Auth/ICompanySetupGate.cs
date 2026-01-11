namespace UaeEInvoice.Services.Auth;

public interface ICompanySetupGate
{
    Task<CompanySetupStatus> GetStatusAsync(bool forceRefresh = false);
    Task<bool> IsSetupCompleteAsync(bool forceRefresh = false);
}

public sealed record CompanySetupStatus(
    bool IsAuthenticated,
    int CompanyId,
    bool HasCompanyClaim,
    bool IsSetupComplete
);
