using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Realms.Api.Data;
using Realms.Api.Dtos;
using Realms.Api.Models;

namespace Realms.Api.Controllers;

[ApiController]
[Route("locations")]
public class LocationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public LocationsController(AppDbContext db) => _db = db;

    // Per ora: identità utente via header (semplice per test)
    // Più avanti: sostituiamo con Firebase ID token.
    [HttpPost("update")]
    public async Task<IActionResult> Update(
    [FromHeader(Name = "X-User-Id")] string userId,
    [FromBody] UpdateLocationRequest req)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("Missing X-User-Id header");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            user = new User { Id = userId, CreatedAtUtc = DateTime.UtcNow };
            _db.Users.Add(user);
        }

        var loc = await _db.UserLocations.FirstOrDefaultAsync(x => x.UserId == userId);
        if (loc is null)
        {
            loc = new UserLocation
            {
                UserId = userId,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.UserLocations.Add(loc);
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
