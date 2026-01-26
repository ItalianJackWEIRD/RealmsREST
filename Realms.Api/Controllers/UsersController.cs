using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Realms.Api.Data;
using Realms.Api.Dtos;

namespace Realms.Api.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet("nearby")]
    public async Task<ActionResult<List<NearbyUserResponse>>> Nearby(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] double radiusMeters = 500,
        [FromQuery] int max = 50
    )
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        var list = await _db.UserLocations
            .AsNoTracking()
            .Where(x => x.UpdatedAtUtc >= cutoff)
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
