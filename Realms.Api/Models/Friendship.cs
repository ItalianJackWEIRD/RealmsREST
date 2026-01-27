namespace Realms.Api.Models;

// Relazione simmetrica (1 riga = 1 amicizia)
public class Friendship
{
    public string UserA { get; set; } = default!;
    public string UserB { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
