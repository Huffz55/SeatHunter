namespace SeatHunter.Application.DTOs;

public record AdminUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Tier,
    string Role,
    bool IsActive,
    int ActiveHuntCount,
    int TotalHuntCount,
    DateTime CreatedAt
);
