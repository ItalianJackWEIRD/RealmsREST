using Microsoft.EntityFrameworkCore;
using Realms.Api.Models;

namespace Realms.Api.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserLocation> UserLocations => Set<UserLocation>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Post> Posts => Set<Post>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder model)
    {
        // USERS
        model.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Username).HasColumnName("username");
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.LastName).HasColumnName("last_name");
            e.Property(x => x.Bio).HasColumnName("bio");
            e.Property(x => x.ProfilePhotoUrl).HasColumnName("profile_photo_url");
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

            e.HasIndex(x => x.Username).IsUnique();
        });

        // LOCATIONS (last location)
        model.Entity<UserLocation>(e =>
        {
            e.ToTable("user_locations");
            e.HasKey(x => x.UserId);

            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

            e.HasOne(x => x.User)
             .WithOne(x => x.Location)
             .HasForeignKey<UserLocation>(x => x.UserId);
        });

        // FRIEND REQUESTS
        model.Entity<FriendRequest>(e =>
        {
            e.ToTable("friend_requests");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FromUserId).HasColumnName("from_user_id");
            e.Property(x => x.ToUserId).HasColumnName("to_user_id");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            e.Property(x => x.RespondedAtUtc).HasColumnName("responded_at_utc");

            e.HasIndex(x => new { x.FromUserId, x.ToUserId }).IsUnique();
        });

        // FRIENDSHIPS (symmetric)
        model.Entity<Friendship>(e =>
        {
            e.ToTable("friendships");
            e.HasKey(x => new { x.UserA, x.UserB });

            e.Property(x => x.UserA).HasColumnName("user_a");
            e.Property(x => x.UserB).HasColumnName("user_b");
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        });

        // POSTS
        model.Entity<Post>(e =>
        {
            e.ToTable("posts");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OwnerUserId).HasColumnName("owner_user_id");
            e.Property(x => x.Caption).HasColumnName("caption");
            e.Property(x => x.PhotoUrl).HasColumnName("photo_url");
            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.Visibility).HasColumnName("visibility");
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            e.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");

            e.HasOne(x => x.Owner)
             .WithMany(x => x.Posts)
             .HasForeignKey(x => x.OwnerUserId);

            e.HasIndex(x => x.ExpiresAtUtc);
        });
    }
}
