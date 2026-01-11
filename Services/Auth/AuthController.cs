using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UaeEInvoice.Data;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SessionService _sessions;

    public AuthController(UserManager<ApplicationUser> userManager, SessionService sessions)
    {
        _userManager = userManager;
        _sessions = sessions;
    }

    public class LoginRequest
    {
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Username & password required" });

        var user = await _userManager.FindByNameAsync(req.UserName)
                   ?? await _userManager.FindByEmailAsync(req.UserName);

        if (user == null)
            return Unauthorized(new { message = "Invalid username/password" });

        var ok = await _userManager.CheckPasswordAsync(user, req.Password);
        if (!ok)
            return Unauthorized(new { message = "Invalid username/password" });

        // ⚠️ ApplicationUser-ல CompanyId property இருந்தா use பண்ணு
        var companyId = user.CompanyId;

        var session = await _sessions.CreateSessionAsync(user.Id, companyId, user.UserName ?? user.Email ?? "");

        return Ok(new
        {
            message = "ok",
            guid = session.SessionGuid,
            companyId = session.CompanyId,
            userName = session.UserName,
            expiresOnUtc = session.ExpiresOnUtc
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromHeader(Name = "X-Session")] string guid)
    {
        var ok = await _sessions.LogoutAsync(guid);
        if (!ok) return Unauthorized(new { message = "Invalid Session" });
        return Ok(new { message = "logged out" });
    }
}
