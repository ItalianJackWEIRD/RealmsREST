using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Realms.Api.Data;
using Realms.Api.Models;

namespace Realms.Api.Controllers;

[ApiController]
[Route("posts")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PostsController(AppDbContext db) => _db = db;

    public record CreatePostRequest(
        string? Caption,
        string? PhotoUrl,
        double Latitude,
        double Longitude,
        string Visibility // "PUBLIC" | "FRIENDS"
    );

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest req)
    {
        var userId = User.Identity?.Name!;
        var now = DateTime.UtcNow;

        if (req.Caption != null)
        {
            var words = req.Caption.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 100) return BadRequest("Caption max 100 words");
        }

        var visibility = (req.Visibility ?? "PUBLIC").ToUpperInvariant();
        if (visibility != "PUBLIC" && visibility != "FRIENDS")
            return BadRequest("Visibility must be PUBLIC or FRIENDS");

        var post = new Post
        {
            OwnerUserId = userId,
            Caption = req.Caption,
            PhotoUrl = req.PhotoUrl,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            Visibility = visibility,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(24),
            IsDeleted = false
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        return Ok(new { post.Id, post.ExpiresAtUtc });
    }

    // Post su mappa:
    // - PUBLIC: visibile a tutti
    // - FRIENDS: visibile solo se owner è amico
    [HttpGet("map")]
    public async Task<ActionResult<List<object>>> Map(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] double radiusMeters = 1000,
        [FromQuery] int max = 100
    )
    {
        var userId = User.Identity?.Name!;
        var now = DateTime.UtcNow;

        var friendIds = await _db.Friendships
            .AsNoTracking()
            .Where(f => f.UserA == userId || f.UserB == userId)
            .Select(f => f.UserA == userId ? f.UserB : f.UserA)
            .ToListAsync();

        var posts = await _db.Posts
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.ExpiresAtUtc > now)
            .ToListAsync();

        var filtered = posts
            .Select(p => new
            {
                Post = p,
                Dist = HaversineMeters(lat, lon, p.Latitude, p.Longitude)
            })
            .Where(x =>
                x.Dist <= radiusMeters &&
                (
                    x.Post.Visibility == "PUBLIC" ||
                    x.Post.OwnerUserId == userId ||
                    (x.Post.Visibility == "FRIENDS" && friendIds.Contains(x.Post.OwnerUserId))
                )
            )
            .OrderBy(x => x.Dist)
            .Take(max)
            .ToList();

        // arricchiamo con owner (per CTA "chiedi amicizia")
        var ownerIds = filtered.Select(x => x.Post.OwnerUserId).Distinct().ToList();
        var owners = await _db.Users
            .AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.FirstName, u.LastName, u.ProfilePhotoUrl })
            .ToListAsync();

        var ownerMap = owners.ToDictionary(o => o.Id, o => o);

        var result = filtered.Select(x => new
        {
            x.Post.Id,
            x.Post.OwnerUserId,
            Owner = ownerMap.TryGetValue(x.Post.OwnerUserId, out var o) ? o : null,
            x.Post.Caption,
            x.Post.PhotoUrl,
            x.Post.Visibility,
            x.Post.Latitude,
            x.Post.Longitude,
            x.Post.CreatedAtUtc,
            x.Post.ExpiresAtUtc
        });

        return Ok(result);
    }

    [HttpGet("feed")]
public async Task<ActionResult<List<object>>> Feed([FromQuery] int max = 100)
{
    var userId = User.Identity?.Name!;
    var now = DateTime.UtcNow;

    // prendo gli id amici
    var friendIds = await _db.Friendships
        .AsNoTracking()
        .Where(f => f.UserA == userId || f.UserB == userId)
        .Select(f => f.UserA == userId ? f.UserB : f.UserA)
        .ToListAsync();

    if (friendIds.Count == 0)
        return Ok(new List<object>());

    // prendo i post attivi degli amici
    var posts = await _db.Posts
        .AsNoTracking()
        .Where(p =>
            !p.IsDeleted &&
            p.ExpiresAtUtc > now &&
            friendIds.Contains(p.OwnerUserId)
        )
        .OrderByDescending(p => p.CreatedAtUtc)
        .Take(max)
        .ToListAsync();

    // prendo owner (username ecc.)
    var ownerIds = posts.Select(p => p.OwnerUserId).Distinct().ToList();

    var owners = await _db.Users
        .AsNoTracking()
        .Where(u => ownerIds.Contains(u.Id))
        .Select(u => new {
            u.Id,
            u.Username,
            u.FirstName,
            u.LastName,
            u.ProfilePhotoUrl
        })
        .ToListAsync();

    var ownerMap = owners.ToDictionary(o => o.Id, o => o);

    // JSON compatibile con MapPostDto
    return Ok(posts.Select(p => new
    {
        id = p.Id,
        ownerUserId = p.OwnerUserId,
        owner = ownerMap.TryGetValue(p.OwnerUserId, out var o) ? o : null,
        caption = p.Caption,
        photoUrl = p.PhotoUrl,
        visibility = p.Visibility,
        latitude = p.Latitude,
        longitude = p.Longitude,
        createdAtUtc = p.CreatedAtUtc,
        expiresAtUtc = p.ExpiresAtUtc
    }));
}




    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        var userId = User.Identity?.Name!;
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);

        if (post is null) return NotFound();
        if (post.OwnerUserId != userId) return Forbid();

        post.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Ok();
    }


    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<object>>> UserPosts(
    [FromRoute] string userId,
    [FromQuery] int max = 100
)
    {
        var me = User.Identity?.Name!;
        var now = DateTime.UtcNow;

        // amici di me (serve per sapere se posso vedere FRIENDS)
        var friendIds = await _db.Friendships
            .AsNoTracking()
            .Where(f => f.UserA == me || f.UserB == me)
            .Select(f => f.UserA == me ? f.UserB : f.UserA)
            .ToListAsync();

        var canSeeFriendsPostsOfThatUser =
            (userId == me) || friendIds.Contains(userId);

        // se non sono amico e non sono io: vedo solo PUBLIC
        var postsQ = _db.Posts
            .AsNoTracking()
            .Where(p =>
                !p.IsDeleted &&
                p.ExpiresAtUtc > now &&
                p.OwnerUserId == userId &&
                (p.Visibility == "PUBLIC" || (canSeeFriendsPostsOfThatUser && p.Visibility == "FRIENDS"))
            )
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(max);

        var posts = await postsQ.ToListAsync();

        // owner info (uno solo)
        var owner = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Username, u.FirstName, u.LastName, u.ProfilePhotoUrl })
            .FirstOrDefaultAsync();

        var result = posts.Select(p => new
        {
            p.Id,
            p.OwnerUserId,
            Owner = owner,
            p.Caption,
            p.PhotoUrl,
            p.Visibility,
            p.Latitude,
            p.Longitude,
            p.CreatedAtUtc,
            p.ExpiresAtUtc
        });

        return Ok(result);
    }



    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        static double ToRad(double d) => d * Math.PI / 180.0;

        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);

        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
