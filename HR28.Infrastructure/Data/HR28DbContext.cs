using HR28.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Data;

public class HR28DbContext : DbContext
{
    public HR28DbContext(DbContextOptions<HR28DbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserScope>()
            .HasKey(us => new
            {
                us.UserId,
                us.ConstituencyId,
                us.IslandId
            });
        modelBuilder.Entity<UserScope>()
    .HasOne(us => us.Constituency)
    .WithMany(c => c.UserScopes)
    .HasForeignKey(us => us.ConstituencyId)
    .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserScope>()
            .HasOne(us => us.Island)
            .WithMany(i => i.UserScopes)
            .HasForeignKey(us => us.IslandId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<UserScope> UserScopes => Set<UserScope>();

    public DbSet<Constituency> Constituencies => Set<Constituency>();

    public DbSet<Island> Islands => Set<Island>();

    public DbSet<OtpRequest> OtpRequests => Set<OtpRequest>();

    public DbSet<AuthorizationCodeHistory> AuthorizationCodeHistories => Set<AuthorizationCodeHistory>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}