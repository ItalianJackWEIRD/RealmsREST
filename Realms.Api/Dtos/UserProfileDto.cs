namespace Realms.Api.Dtos;

public record UserProfileDto(
    string Id,
    string Username,
    string FirstName,
    string LastName,
    string? Bio,
    string? ProfilePhotoUrl,
    int FriendsCount,
    bool IsFriend
);
