using HR28.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using HR28.Domain.Entities;
namespace HR28.Infrastructure.Data;

public class HR28DbContext : DbContext
{
    public HR28DbContext(
        DbContextOptions<HR28DbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --------------------------------------------------
        // User and role relationships
        // --------------------------------------------------

        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new
            {
                ur.UserId,
                ur.RoleId
            });

        // --------------------------------------------------
        // User scope relationships
        // --------------------------------------------------

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

        // --------------------------------------------------
        // Constituency and island many-to-many relationship
        // --------------------------------------------------

        modelBuilder.Entity<ConstituencyIsland>()
            .HasKey(ci => new
            {
                ci.ConstituencyId,
                ci.IslandId
            });

        modelBuilder.Entity<ConstituencyIsland>()
            .HasOne(ci => ci.Constituency)
            .WithMany(c => c.ConstituencyIslands)
            .HasForeignKey(ci => ci.ConstituencyId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ConstituencyIsland>()
            .HasOne(ci => ci.Island)
            .WithMany(i => i.ConstituencyIslands)
            .HasForeignKey(ci => ci.IslandId)
            .OnDelete(DeleteBehavior.NoAction);

        /*
         * Transitional configuration:
         *
         * Island.ConstituencyId remains in the entity for now.
         * EF Core will continue configuring that existing
         * one-to-many relationship by convention.
         *
         * Do not remove ConstituencyId from Island.cs until:
         * 1. ConstituencyIslands has been populated.
         * 2. Scope queries use ConstituencyIslands.
         * 3. Existing records have been verified.
         */

        // --------------------------------------------------
        // Encounter relationships
        // --------------------------------------------------

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

        // --------------------------------------------------
        // Pledge relationships
        // --------------------------------------------------

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

        // --------------------------------------------------
        // Voter and influencer relationships
        // --------------------------------------------------

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

        // --------------------------------------------------
        // Influencer geography relationships
        // --------------------------------------------------

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

        // --------------------------------------------------
        // Voter indexes
        // --------------------------------------------------

        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.NationalId)
            .IsUnique();

        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.ConstituencyCode);

        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.RegisteredIsland);

        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.AtollCode);

        /*
         * Do not add the unique Constituency.Code index yet.
         *
         * Existing manually created constituency records still
         * have null codes. Add that index only after those records
         * have been reconciled with the 93 official constituencies.
         */
    }

    // --------------------------------------------------
    // DbSets
    // --------------------------------------------------

    public DbSet<User> Users =>
        Set<User>();

    public DbSet<Role> Roles =>
        Set<Role>();

    public DbSet<UserRole> UserRoles =>
        Set<UserRole>();

    public DbSet<UserScope> UserScopes =>
        Set<UserScope>();

    public DbSet<Constituency> Constituencies =>
        Set<Constituency>();

    public DbSet<Island> Islands =>
        Set<Island>();

    public DbSet<ConstituencyIsland> ConstituencyIslands =>
        Set<ConstituencyIsland>();

    public DbSet<OtpRequest> OtpRequests =>
        Set<OtpRequest>();

    public DbSet<AuthorizationCodeHistory>
        AuthorizationCodeHistories =>
            Set<AuthorizationCodeHistory>();

    public DbSet<AuditLog> AuditLogs =>
        Set<AuditLog>();

    public DbSet<Voter> Voters =>
        Set<Voter>();

    public DbSet<Encounter> Encounters =>
        Set<Encounter>();

    public DbSet<Pledge> Pledges =>
        Set<Pledge>();

    public DbSet<Influencer> Influencers =>
        Set<Influencer>();

    public DbSet<VoterInfluencer> VoterInfluencers =>
        Set<VoterInfluencer>();

    public Guid? CurrentUserId { get; set; }

    

}