using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatHunter.Application.Interfaces;
using System.Security.Claims;

namespace SeatHunter.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IAuthService _authService;

    public UserController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _authService.ChangePasswordAsync(userId, request.OldPassword, request.NewPassword);
        if (!result)
            return BadRequest(new { message = "Mevcut şifre yanlış veya işlem başarısız." });

        return Ok(new { message = "Şifre başarıyla değiştirildi." });
    }

    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _authService.DeleteAccountAsync(userId);
        if (!result)
            return BadRequest(new { message = "Hesap silinirken bir hata oluştu." });

        return Ok(new { message = "Hesap başarıyla silindi." });
    }
}

public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
