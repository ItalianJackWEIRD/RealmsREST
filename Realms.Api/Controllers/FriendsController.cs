using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Realms.Api.Data;
using Realms.Api.Models;

namespace Realms.Api.Controllers;

[ApiController]
[Route("friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly AppDbContext _db;
    public FriendsController(AppDbContext db) => _db = db;

    public record SendFriendRequestRequest(string ToUserId);

    [HttpPost("requests")]
    public async Task<IActionResult> SendRequest([FromBody] SendFriendRequestRequest req)
    {
        var fromUserId = User.Identity?.Name!;
        if (string.IsNullOrWhiteSpace(req.ToUserId) || req.ToUserId == fromUserId)
            return BadRequest();

        // non creare richieste se già amici
        var alreadyFriends = await AreFriends(fromUserId, req.ToUserId);
        if (alreadyFriends) return Conflict("Already friends");

        // evita duplicati pending
        var pending = await _db.FriendRequests.AnyAsync(x =>
            x.FromUserId == fromUserId && x.ToUserId == req.ToUserId && x.Status == "PENDING");

        if (pending) return Conflict("Request already pending");

        _db.FriendRequests.Add(new FriendRequest
        {
            FromUserId = fromUserId,
            ToUserId = req.ToUserId,
            Status = "PENDING",
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<List<object>>> ListFriends()
    {
        var userId = User.Identity?.Name!;

        var friendIds = await _db.Friendships
            .AsNoTracking()
            .Where(f => f.UserA == userId || f.UserB == userId)
            .Select(f => f.UserA == userId ? f.UserB : f.UserA)
            .ToListAsync();

        var friends = await _db.Users
            .AsNoTracking()
            .Where(u => friendIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.FirstName,
                u.LastName,
                u.ProfilePhotoUrl
            })
            .ToListAsync();

        return Ok(friends);
    }

    [HttpGet("requests/incoming")]
    public async Task<ActionResult<List<FriendRequest>>> Incoming()
    {
        var userId = User.Identity?.Name!;
        var list = await _db.FriendRequests
            .AsNoTracking()
            .Where(r => r.ToUserId == userId && r.Status == "PENDING")
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost("requests/{id:long}/accept")]
    public async Task<IActionResult> Accept(long id)
    {
        var userId = User.Identity?.Name!;

        var req = await _db.FriendRequests.FirstOrDefaultAsync(r => r.Id == id && r.ToUserId == userId);
        if (req is null) return NotFound();
        if (req.Status != "PENDING") return Conflict("Not pending");

        // chiudi request
        req.Status = "ACCEPTED";
        req.RespondedAtUtc = DateTime.UtcNow;

        // crea friendship simmetrica (ordinata)
        var a = string.Compare(req.FromUserId, req.ToUserId, StringComparison.Ordinal) < 0 ? req.FromUserId : req.ToUserId;
        var b = a == req.FromUserId ? req.ToUserId : req.FromUserId;

        // evita duplicati
        var exists = await _db.Friendships.AnyAsync(f => f.UserA == a && f.UserB == b);
        if (!exists)
            _db.Friendships.Add(new Friendship { UserA = a, UserB = b, CreatedAtUtc = DateTime.UtcNow });

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("requests/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id)
    {
        var userId = User.Identity?.Name!;

        var req = await _db.FriendRequests.FirstOrDefaultAsync(r => r.Id == id && r.ToUserId == userId);
        if (req is null) return NotFound();
        if (req.Status != "PENDING") return Conflict("Not pending");

        req.Status = "REJECTED";
        req.RespondedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    // Rimozione amicizia: SÌ
    [HttpDelete("{friendUserId}")]
    public async Task<IActionResult> Remove(string friendUserId)
    {
        var userId = User.Identity?.Name!;
        if (friendUserId == userId) return BadRequest();

        var a = string.Compare(userId, friendUserId, StringComparison.Ordinal) < 0 ? userId : friendUserId;
        var b = a == userId ? friendUserId : userId;

        var friendship = await _db.Friendships.FindAsync(a, b);
        if (friendship is null) return NotFound();

        _db.Friendships.Remove(friendship);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private async Task<bool> AreFriends(string userId, string otherUserId)
    {
        var a = string.Compare(userId, otherUserId, StringComparison.Ordinal) < 0 ? userId : otherUserId;
        var b = a == userId ? otherUserId : userId;
        return await _db.Friendships.AnyAsync(f => f.UserA == a && f.UserB == b);
    }
}
