using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatHunter.Application.Interfaces;

using SeatHunter.Domain.Constants;

namespace SeatHunter.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(AppConstants.RateLimiting.FixedPolicy)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var token = await _authService.LoginAsync(request.Email, request.Password);
        if (token == null)
            return Unauthorized(new { message = "Geçersiz e-posta veya şifre." });

        return Ok(new { token });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var success = await _authService.RegisterAsync(request.Email, request.Password, request.FirstName, request.LastName);
        if (!success)
            return BadRequest(new { message = "Bu e-posta adresi zaten kullanılıyor." });

        return Ok(new { message = "Kayıt başarılı." });
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
