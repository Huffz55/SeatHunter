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
    private readonly IUserRepository _userRepository;

    public UserController(IAuthService authService, IUserRepository userRepository)
    {
        _authService = authService;
        _userRepository = userRepository;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive) return Unauthorized(new { message = "Hesabınız askıya alınmıştır veya oturumunuz geçersizdir." });

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            Tier = user.Tier.ToString(),
            Role = user.Role,
            IsAdmin = user.Role == "Admin",
            user.IsActive,
            user.CreatedAt
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return BadRequest(new { message = "Ad ve soyad alanları boş bırakılamaz." });
        }

        if (firstName.Length > 100 || lastName.Length > 100)
        {
            return BadRequest(new { message = "Ad ve soyad en fazla 100 karakter olabilir." });
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive) return Unauthorized();

        user.FirstName = firstName;
        user.LastName = lastName;
        await _userRepository.UpdateAsync(user, cancellationToken);

        return Ok(new
        {
            message = "Profil bilgileriniz güncellendi.",
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            Tier = user.Tier.ToString(),
            Role = user.Role,
            IsAdmin = user.Role == "Admin"
        });
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

public class UpdateProfileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
