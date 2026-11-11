using System.Threading.Tasks;
using SeatHunter.Application.DTOs;

namespace SeatHunter.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto?> LoginAsync(string email, string password);
    Task<bool> RegisterAsync(string email, string password, string firstName, string lastName);
    Task<bool> ChangePasswordAsync(string userId, string oldPassword, string newPassword);
    Task<bool> DeleteAccountAsync(string userId);
}
