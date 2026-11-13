
using Identity.Api.DTOs;
using Identity.Api.Models;
using Identity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenService _tokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        TokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (request.Role != "Buyer" && request.Role != "Seller")
        {
            return BadRequest(new
            {
                message = "Registration role must be Buyer or Seller."
            });
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);

        if (existingUser != null)
        {
            return Conflict(new
            {
                message = "Email is already registered."
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(
            user,
            request.Password
        );

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(e => e.Description)
            });
        }

        var roleResult = await _userManager.AddToRoleAsync(
            user,
            request.Role
        );

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            return StatusCode(500, new
            {
                message = "Could not assign user role."
            });
        }

        var (token, expiresAt) =
            await _tokenService.CreateTokenAsync(user);

        return Ok(new AuthResponse(
            token,
            expiresAt,
            user.Email!,
            request.Role
        ));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null ||
            !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var roles = await _userManager.GetRolesAsync(user);

        var (token, expiresAt) =
            await _tokenService.CreateTokenAsync(user);

        return Ok(new AuthResponse(
            token,
            expiresAt,
            user.Email!,
            roles.FirstOrDefault() ?? string.Empty
        ));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            Roles = roles
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin-check")]
    public IActionResult AdminCheck()
    {
        return Ok(new
        {
            message = "You have access to the admin area."
        });
    }
}
