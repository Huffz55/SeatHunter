namespace SeatHunter.Application.DTOs;

public record AuthResultDto(
    string Token, 
    string UserId, 
    string Email, 
    string FirstName, 
    string LastName, 
    string Tier, 
    string Role, 
    bool IsAdmin
);
