namespace Realms.Api.Models;

public class Post
{
    public long Id { get; set; }

    public string OwnerUserId { get; set; } = default!;
    public User Owner { get; set; } = default!;

    // max 100 parole → validazione API
    public string? Caption { get; set; }

    // URL Cloud Storage
    public string? PhotoUrl { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // PUBLIC / FRIENDS
    public string Visibility { get; set; } = "PUBLIC";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }

    public bool IsDeleted { get; set; } = false;
}
