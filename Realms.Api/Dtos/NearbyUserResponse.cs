namespace Realms.Api.Dtos;

public record NearbyUserResponse(
    string UserId,
    double Latitude,
    double Longitude,
    DateTime UpdatedAtUtc
);
