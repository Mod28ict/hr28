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
            .HasKey(us => us.Id);

        modelBuilder.Entity<UserScope>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserScopes)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.NoAction);

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

        modelBuilder.Entity<Encounter>()
            .HasOne(e => e.Voter)
            .WithMany(v => v.Encounters)
            .HasForeignKey(e => e.VoterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Encounter>()
            .HasOne(e => e.RecordedByUser)
            .WithMany(u => u.RecordedEncounters)
            .HasForeignKey(e => e.RecordedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Pledge>()
            .HasOne(p => p.Voter)
            .WithMany(v => v.Pledges)
            .HasForeignKey(p => p.VoterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Pledge>()
            .HasOne(p => p.CreatedByUser)
            .WithMany(u => u.CreatedPledges)
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Pledge>()
            .HasOne(p => p.AssignedToUser)
            .WithMany(u => u.AssignedPledges)
            .HasForeignKey(p => p.AssignedToUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<VoterInfluencer>()
            .HasOne(vi => vi.Voter)
            .WithMany(v => v.Influencers)
            .HasForeignKey(vi => vi.VoterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VoterInfluencer>()
            .HasOne(vi => vi.Influencer)
            .WithMany(i => i.Voters)
            .HasForeignKey(vi => vi.InfluencerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Influencer>()
            .HasOne(i => i.Constituency)
            .WithMany()
            .HasForeignKey(i => i.ConstituencyId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Influencer>()
            .HasOne(i => i.Island)
            .WithMany()
            .HasForeignKey(i => i.IslandId)
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
    public DbSet<Voter> Voters => Set<Voter>();
    public DbSet<Encounter> Encounters => Set<Encounter>();

    public DbSet<Pledge> Pledges => Set<Pledge>();

    public DbSet<Influencer> Influencers => Set<Influencer>();

    public DbSet<VoterInfluencer> VoterInfluencers => Set<VoterInfluencer>();
}