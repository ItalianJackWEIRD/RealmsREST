namespace Realms.Api.Models;

public class UserLocation
{
    public string UserId { get; set; } = default!;
    public User User { get; set; } = default!;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
