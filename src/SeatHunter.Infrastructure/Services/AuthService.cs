using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SeatHunter.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ITelegramService _telegramService;

    public AuthService(IUserRepository userRepository, IConfiguration configuration, ITelegramService telegramService)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _telegramService = telegramService;
    }

    public async Task<string?> LoginAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null || !VerifyPassword(password, user.PasswordHash))
        {
            return null; // Auth failed
        }

        return GenerateJwtToken(user);
    }

    public async Task<bool> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        var existingUser = await _userRepository.GetByEmailAsync(email);
        if (existingUser != null)
            return false;

        var user = new User
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = HashPassword(password)
        };

        await _userRepository.AddAsync(user);

        // Telegram admin monitor alert
        _ = Task.Run(() => _telegramService.SendMessageAsync($"👤 Yeni Üye Kaydı: {user.FirstName} {user.LastName} ({user.Email}) sisteme katıldı."));

        return true;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string oldPassword, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || !VerifyPassword(oldPassword, user.PasswordHash))
            return false;

        user.PasswordHash = HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<bool> DeleteAccountAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return false;

        await _userRepository.DeleteAsync(user);
        return true;
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    private bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName)
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
