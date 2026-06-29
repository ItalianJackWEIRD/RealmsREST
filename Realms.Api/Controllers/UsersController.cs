using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Realms.Api.Data;
using Realms.Api.Models;
using Realms.Api.Dtos;
using System.Security.Claims;
using Google.Cloud.Storage.V1;

namespace Realms.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly StorageClient? _storageClient;
    private readonly UrlSigner? _urlSigner;
    private readonly IConfiguration _config;
    public UsersController(
        AppDbContext db,
        StorageClient? storageClient,
        UrlSigner? urlSigner,
        IConfiguration config)
    {
        _db = db;
        _storageClient = storageClient;
        _urlSigner = urlSigner;
        _config = config;
    }

    // DTO (evitiamo di esporre l'entità EF "User" direttamente)
    public record MeResponse(
        string Id,
        string Username,
        string FirstName,
        string LastName,
        string? Bio,
        string? ProfilePhotoUrl,
        int FriendsCount
    );

    public record UpdateMeRequest(
        string Username,
        string FirstName,
        string LastName,
        string? Bio,
        string? ProfilePhotoUrl
    );



    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = GetUid();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return NotFound();

        var friendsCount = await _db.Friendships.CountAsync(f => f.UserA == userId || f.UserB == userId);

        return Ok(new MeResponse(
            user.Id, user.Username, user.FirstName, user.LastName, user.Bio, user.ProfilePhotoUrl, friendsCount
        ));
    }

    [HttpPost("me")]
    public async Task<IActionResult> CreateMe([FromBody] CreateMeRequest req)
    {
        var userId = GetUid();

        var username = req.Username?.Trim();
        var firstName = req.FirstName?.Trim();
        var lastName = req.LastName?.Trim();

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName))
            return BadRequest("username/firstName/lastName required");

        var exists = await _db.Users.AnyAsync(u => u.Id == userId);
        if (exists) return Ok(); // idempotente

        var usernameTaken = await _db.Users.AnyAsync(u => u.Username == username);
        if (usernameTaken) return Conflict("username already taken");

        var user = new User
        {
            Id = userId,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            Bio = req.Bio,
            ProfilePhotoUrl = req.ProfilePhotoUrl,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok();
    }


    public record CreateMeRequest(
        string Username,
        string FirstName,
        string LastName,
        string? Bio,
        string? ProfilePhotoUrl
    );


    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest req)
    {
        var userId = GetUid();

        var newUsername = req.Username?.Trim();
        if (string.IsNullOrWhiteSpace(newUsername)) return BadRequest("username required");

        var taken = await _db.Users.AnyAsync(u => u.Username == newUsername && u.Id != userId);
        if (taken) return Conflict("username already taken");


        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return NotFound();

        user.Username = newUsername;
        user.FirstName = req.FirstName.Trim();
        user.LastName = req.LastName.Trim();
        user.Bio = req.Bio;
        user.ProfilePhotoUrl = req.ProfilePhotoUrl;

        await _db.SaveChangesAsync();
        return Ok();
    }


    // ========== GET /users/{id}/profile ==========
    [HttpGet("{id}/profile")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> GetProfile(string id)
    {
        var me = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(me)) return Unauthorized();

        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();

        // FriendsCount: conta tutte le amicizie dove l’utente compare in uno dei due lati
        // (adatta i nomi campi in base al tuo model Friendship)
        var friendsCount = await _db.Friendships.CountAsync(f =>
            (f.UserA == id || f.UserB == id)
        );

        // IsFriend: viewer(me) è amico di id?
        var isFriend = await _db.Friendships.AnyAsync(f =>
            (f.UserA == me && f.UserB == id) ||
            (f.UserA == id && f.UserB == me)
        );

        return Ok(new UserProfileDto(
            u.Id,
            u.Username,
            u.FirstName,
            u.LastName,
            u.Bio,
            u.ProfilePhotoUrl,
            friendsCount,
            isFriend
        ));
    }

    // search by username prefix
    public record SearchUserResponse(
        string Id,
        string Username,
        string? ProfilePhotoUrl
    );

    [HttpGet("search")]
    public async Task<ActionResult<List<SearchUserResponse>>> Search([FromQuery] string username, [FromQuery] int max = 20)
    {
        var meId = GetUid();

        username = (username ?? "").Trim();
        if (username.Length < 2) return Ok(new List<SearchUserResponse>());

        var list = await _db.Users
            .AsNoTracking()
            .Where(u =>
                u.Id != meId &&
                u.Username != null &&
                u.Username.Contains(username)
            )
            .OrderBy(u => u.Username)
            .Take(Math.Clamp(max, 1, 50))
            .Select(u => new SearchUserResponse(u.Id, u.Username, u.ProfilePhotoUrl))
            .ToListAsync();

        return Ok(list);
    }


    // Get usernames by ids
    public record UsernamesRequest(List<string> Ids);
    public record UsernameItem(string Id, string Username);
    public record UsernamesResponse(List<UsernameItem> Items);

    // ========== GET /users/usernames ==========
    // ritorna una mappa di username per id
    [HttpPost("usernames")]
    public async Task<ActionResult<UsernamesResponse>> GetUsernames([FromBody] UsernamesRequest req)
    {
        if (req?.Ids is null || req.Ids.Count == 0)
            return Ok(new UsernamesResponse(new List<UsernameItem>()));

        var ids = req.Ids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct()
            .ToList();

        var items = await _db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UsernameItem(u.Id, u.Username))
            .ToListAsync();

        return Ok(new UsernamesResponse(items));
    }


    // ========== GET /users/nearby ==========
    // Ora: SOLO amici (posizioni visibili solo amici)
    [HttpGet("nearby")]
    public async Task<ActionResult<List<NearbyUserResponse>>> Nearby(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] double radiusMeters = 500,
        [FromQuery] int max = 50
    )
    {
        var userId = GetUid();
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        // Lista amici (flatten userA/userB)
        var friendIds = await _db.Friendships
            .AsNoTracking()
            .Where(f => f.UserA == userId || f.UserB == userId)
            .Select(f => f.UserA == userId ? f.UserB : f.UserA)
            .ToListAsync();

        if (friendIds.Count == 0)
            return Ok(new List<NearbyUserResponse>());

        var list = await _db.UserLocations
            .AsNoTracking()
            .Where(x => x.UpdatedAtUtc >= cutoff && friendIds.Contains(x.UserId))
            .ToListAsync();

        var result = list
            .Select(x => new
            {
                x.UserId,
                x.Latitude,
                x.Longitude,
                x.UpdatedAtUtc,
                Dist = HaversineMeters(lat, lon, x.Latitude, x.Longitude)
            })
            .Where(x => x.Dist <= radiusMeters)
            .OrderBy(x => x.Dist)
            .Take(max)
            .Select(x => new NearbyUserResponse(x.UserId, x.Latitude, x.Longitude, x.UpdatedAtUtc))
            .ToList();

        return Ok(result);
    }


    [HttpPost("profile-picture")]
    [Authorize]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        // 1. Validazione base
        if (file == null || file.Length == 0) return BadRequest("File vuoto");
        if (file.Length > 5 * 1024 * 1024) return BadRequest("File troppo grande (max 5MB)");

        // 2. Recuperiamo l'UID di Firebase dell'utente loggato
        // User.Identity.Name contiene l'UID perché abbiamo configurato NameClaimType = "user_id" nel Program.cs
        var userId = User.Identity?.Name;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // 3. Definiamo il percorso nel bucket (es: profiles/abc123.jpg)
        // Usiamo l'estensione originale o forziamo .jpg se hai fatto resize su Android
        var extension = Path.GetExtension(file.FileName).ToLower();
        var objectName = $"profiles/{userId}{extension}";

        // 4. Upload sul Bucket (sovrascrive se esiste già)
        using var stream = file.OpenReadStream();
        await _storageClient.UploadObjectAsync(
            _config["GoogleCloud:BucketName"],
            objectName,
            file.ContentType,
            stream
        );

        // 5. Generiamo l'URL finale
        // Se il bucket è privato (consigliato), generiamo un URL firmato a lunga scadenza (es. 7 giorni)
        // o uno breve se l'app lo richiede spesso.
        var signedUrl = _urlSigner.Sign(
            _config["GoogleCloud:BucketName"],
            objectName,
            TimeSpan.FromDays(7),
            HttpMethod.Get
        );

        // Qui potresti anche salvare 'objectName' nel tuo DB Postgres se vuoi tenere traccia
        // del fatto che l'utente ha una foto.

        return Ok(new
        {
            Message = "Foto caricata con successo",
            Url = signedUrl
        });
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

    // DTO compatibile col tuo progetto (già esiste in Dtos)
    public record NearbyUserResponse(string UserId, double Latitude, double Longitude, DateTime UpdatedAtUtc);



    private string GetUid()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("user_id")
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Missing user id claim");
    }


}
