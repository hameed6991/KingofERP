using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UaeEInvoice.Services.Auth;

namespace UaeEInvoice.Controllers
{
    [ApiController]
    [Route("api")]
    public class LoginController : ControllerBase
    {
        // ✅ GET: /api/login
        // Uses X-Session header (your existing flow) + optional rememberMe
        [HttpGet("login")]
        public async Task<IActionResult> Login(
            [FromHeader(Name = "X-Session")] string guid,
            [FromQuery] bool rememberMe,
            [FromServices] SessionService sessions)
        {
            // ✅ 1) Validate session first
            var s = await sessions.ValidateSessionAsync(guid);
            if (s == null)
                return Unauthorized(new { message = "Invalid Session" });

            // ✅ 2) Build claims from session
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, s.UserId.ToString()),
                new(ClaimTypes.Name, s.UserName ?? ""),
                new("CompanyId", s.CompanyId.ToString())
            };

            // ✅ IMPORTANT: Use the SAME scheme name as SignInAsync
            var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
            var principal = new ClaimsPrincipal(identity);

            // ✅ 3) Sign in (sets auth cookie with claims)
            var props = BuildAuthProperties(rememberMe);

            await HttpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,   // ✅ MUST MATCH Program.cs Identity scheme
                principal,
                props
            );

            return Ok(new
            {
                ok = true,
                companyId = s.CompanyId,
                user = s.UserName,
                rememberMe
            });
        }

        // ✅ POST: /api/login/form
        // This is the FIX for your "RememberMe = false,true" binding crash (checkbox + hidden field).
        // Use this endpoint if you submit login from a form.
        [HttpPost("login/form")]
        public async Task<IActionResult> LoginFromForm(
            [FromForm] string guid,
            [FromForm] bool[] RememberMe, // ✅ handles "false,true"
            [FromServices] SessionService sessions)
        {
            var remember = RememberMe != null && RememberMe.Length > 0 && RememberMe.Last();

            var s = await sessions.ValidateSessionAsync(guid);
            if (s == null)
                return Unauthorized(new { message = "Invalid Session" });

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, s.UserId.ToString()),
                new(ClaimTypes.Name, s.UserName ?? ""),
                new("CompanyId", s.CompanyId.ToString())
            };

            var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal,
                BuildAuthProperties(remember)
            );

            return Ok(new
            {
                ok = true,
                companyId = s.CompanyId,
                user = s.UserName,
                rememberMe = remember
            });
        }

        // ✅ Optional: logout endpoint
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Ok(new { ok = true });
        }

        private static AuthenticationProperties BuildAuthProperties(bool rememberMe)
        {
            // If rememberMe false -> session cookie (expires when browser closes)
            // If true -> persistent cookie (e.g., 30 days)
            return new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null,
                AllowRefresh = true
            };
        }
    }
}
