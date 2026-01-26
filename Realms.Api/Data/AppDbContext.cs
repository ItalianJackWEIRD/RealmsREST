using Microsoft.EntityFrameworkCore;
using Realms.Api.Models;

namespace Realms.Api.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserLocation> UserLocations => Set<UserLocation>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DisplayName).HasColumnName("display_name");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

            e.HasOne(x => x.Location)
             .WithOne(x => x.User)
             .HasForeignKey<UserLocation>(x => x.UserId);
        });

        modelBuilder.Entity<UserLocation>(e =>
        {
            e.ToTable("user_locations");
            e.HasKey(x => x.UserId);

            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });
    }
}
