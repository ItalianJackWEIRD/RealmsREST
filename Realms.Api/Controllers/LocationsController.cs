using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Realms.Api.Data;
using Realms.Api.Dtos;
using Realms.Api.Models;

namespace Realms.Api.Controllers;

[ApiController]
[Route("locations")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public LocationsController(AppDbContext db) => _db = db;

    [HttpPost("update")]
    public async Task<IActionResult> Update([FromBody] UpdateLocationRequest req)
    {
        var userId = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        // Auto-create user row (minimo) se non esiste:
        // serve per non rompere l'app appena l'utente logga ma non ha ancora compilato il profilo
        var userExists = await _db.Users.AnyAsync(x => x.Id == userId);
        if (!userExists)
        {
            _db.Users.Add(new User
            {
                Id = userId,
                CreatedAtUtc = DateTime.UtcNow,

                // Campi profilo: li completerà poi con PUT /users/me
                Username = $"user_{userId[..Math.Min(8, userId.Length)]}",
                FirstName = "N/A",
                LastName = "N/A",
                Bio = null,
                ProfilePhotoUrl = null
            });
        }

        var loc = await _db.UserLocations.FirstOrDefaultAsync(x => x.UserId == userId);
        if (loc is null)
        {
            _db.UserLocations.Add(new UserLocation
            {
                UserId = userId,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            loc.Latitude = req.Latitude;
            loc.Longitude = req.Longitude;
            loc.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }
}
