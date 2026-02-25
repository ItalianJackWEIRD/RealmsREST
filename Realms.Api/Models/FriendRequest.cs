namespace Realms.Api.Models;

public class FriendRequest
{
    public long Id { get; set; }

    public string FromUserId { get; set; } = default!;
    public string ToUserId { get; set; } = default!;

    // PENDING / ACCEPTED / REJECTED
    public string Status { get; set; } = "PENDING";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
}
