namespace Realms.Api.Models;

public class User
{
    public string Id { get; set; } = default!; // Firebase UID o ID string
    public string? DisplayName { get; set; }
    public string? Email { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public UserLocation? Location { get; set; }
}
