namespace Realms.Api.Models;

public class User
{
    // Firebase UID
    public string Id { get; set; } = default!;

    public string Username { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Bio { get; set; }

    // URL Cloud Storage
    public string? ProfilePhotoUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public UserLocation? Location { get; set; }

    public ICollection<Post> Posts { get; set; } = new List<Post>();
}
