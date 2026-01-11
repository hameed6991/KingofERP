using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UaeEInvoice.Controllers.Dtos;
using UaeEInvoice.Data;

namespace UaeEInvoice.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly RoleManager<IdentityRole> _roleMgr;

    public AdminUsersController(UserManager<ApplicationUser> userMgr, RoleManager<IdentityRole> roleMgr)
    {
        _userMgr = userMgr;
        _roleMgr = roleMgr;
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Body required" });

        var email = (dto.Email ?? "").Trim().ToLower();
        var password = dto.Password ?? "";
        var role = (dto.Role ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email required" });

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return BadRequest(new { message = "Password min 6 chars" });

        if (dto.CompanyId <= 0)
            return BadRequest(new { message = "CompanyId required" });

        // Check if user exists
        var existing = await _userMgr.FindByEmailAsync(email);
        if (existing != null)
            return Conflict(new { message = "User already exists" });

        // Role check
        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleExists = await _roleMgr.RoleExistsAsync(role);
            if (!roleExists)
                return BadRequest(new { message = $"Role '{role}' not found" });
        }

        // Create
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = dto.EmailConfirmed,
            CompanyId = dto.CompanyId
        };

        var create = await _userMgr.CreateAsync(user, password);
        if (!create.Succeeded)
        {
            return BadRequest(new
            {
                message = "Create failed",
                errors = create.Errors.Select(e => e.Description).ToList()
            });
        }

        // Assign role
        if (!string.IsNullOrWhiteSpace(role))
        {
            var addRole = await _userMgr.AddToRoleAsync(user, role);
            if (!addRole.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Role assign failed",
                    errors = addRole.Errors.Select(e => e.Description).ToList()
                });
            }
        }

        return Ok(new
        {
            message = "User created",
            userId = user.Id,
            email = user.Email,
            companyId = user.CompanyId,
            role = role
        });
    }
}
